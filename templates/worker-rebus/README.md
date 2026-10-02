# Company.Product.Worker

A **message worker** (think Azure Functions with a Service Bus or queue trigger) that runs on any cloud or on-prem. Small, testable **handlers** run when a message arrives:

- an **event** (something happened; any number of services may subscribe), or
- a **command** (do this; sent to this worker's queue)

It is built on [Rebus](https://github.com/rebus-org/Rebus) (MIT), a lean .NET service bus. It's a normal .NET process: no sidecar, no extra runtime. RabbitMQ is the local broker, and production can use any Rebus transport. **The broker is chosen in one file (`Handlers/Setup/MessagingSetup.cs`) and in the `Messaging` settings.**

> **Scheduled work (cron) is not in this template.** A timer fires once per replica, which clashes with autoscaling. An architecture test fails the build if this worker contains a hosted service or timer. Put scheduled jobs in a separate single-replica app that sends commands to this worker.

---

## Contents

1. [How it works](#how-it-works)
2. [Quick start](#quick-start)
3. [The sample handlers](#the-sample-handlers)
4. [Message contracts](#message-contracts)
5. [Switching brokers](#switching-brokers)
6. [Adding a handler](#adding-a-handler)
7. [Reliability: retries, the error queue, idempotency](#reliability-retries-the-error-queue-idempotency)
8. [Configuration](#configuration)
9. [Deploying](#deploying)
10. [Telemetry](#telemetry)
11. [Testing](#testing)
12. [Rebus or Dapr?](#rebus-or-dapr)
13. [Troubleshooting](#troubleshooting)

---

## How it works

```mermaid
flowchart LR
    PUB["Other services<br/>(publish events)"] --> MQ[("Broker<br/>RabbitMQ locally")]
    SND["Anything that sends<br/>commands"] --> MQ
    subgraph Pod["Worker pod (1..N replicas, KEDA)"]
        R["Rebus<br/>retries, error queue"]
        H["Handlers -> Managers -> Engines / Accessors"]
    end
    MQ -->|"queue: worker"| R
    R --> H
    H -->|"IHttpClientFactory"| API["Orders API<br/>(service discovery)"]
    H -->|"IBus.Publish"| MQ
    R -.->|"failed messages"| ERR[("error queue")]
```

| Message | Kind | Handler | Arrives by |
|---------|------|---------|------------|
| `OrderPlacedEvent` | **Event** | `OrderPlacedHandler` | Subscription: the broker routes published events to the `worker` queue |
| `ImportOrderCommand` | **Command** | `ImportOrderHandler` | Sent straight to the `worker` queue |

Inside the worker, the same iDesign layers as the API apply: **Handlers** (thin triggers) call **Managers** (use cases), which call **Engines** (pure logic) and **Accessors** (all I/O, including publishing to the bus). Architecture tests enforce the layering. See [CLAUDE.md](CLAUDE.md).

All replicas read the same queue, so each message goes to exactly one replica (competing consumers). That is what lets the worker scale from 0 to N.

---

## Quick start

**Prerequisites:** .NET 10 SDK, Docker Desktop, VS Code with C# Dev Kit. Run the **orders API** (the `idesign-api` template) on http://localhost:5080 so the handlers have something to call.

### VS Code

Press **F5** and pick **Worker + RabbitMQ (Docker)**. It starts RabbitMQ and the telemetry dashboard (`docker compose up -d --wait`), builds the worker, and runs it on http://localhost:5100. On start the worker creates its `worker` queue and subscribes to `OrderPlacedEvent`.

| URL | What |
|-----|------|
| http://localhost:15672 | RabbitMQ management (guest / guest): queues, exchanges, error queue |
| http://localhost:18888 | Telemetry dashboard: logs and traces for every message |
| http://localhost:5100/metrics | Worker metrics (Prometheus format) |

### Try each handler

Open `samples/requests.http` (with the REST Client extension) and send the requests. They publish through RabbitMQ's management API, with the headers Rebus needs:

| Request | What happens |
|---------|--------------|
| **Command** | `ImportOrderHandler` creates the order through the orders API, then publishes `OrderImported` |
| **Command that fails validation** | Goes to the `error` queue after **one** attempt (fail fast). Look at it in the RabbitMQ UI: the error is in the `rbs2-error-details` header |
| **Event** | `OrderPlacedHandler` fetches the order and sends a notification (the log, for now). An unknown order id is logged and dropped |

### Everything in containers

```bash
docker compose --profile container up --build     # worker health on :8080
```

---

## The sample handlers

**Event: `OrderPlacedHandler`.** When an `OrderPlacedEvent` arrives, it fetches the order from the orders API and sends an "order placed" notification. The notification Accessor writes to the log; replace its body with an email or SMS provider's HTTP API. If the orders API doesn't know the order, the handler logs a warning and returns: the message is acknowledged and dropped, because retrying won't help.

**Command: `ImportOrderHandler`.** Creates the order in `ImportOrderCommand` through the orders API, then publishes an `OrderImported` event through `OrderEventsAccessor`. Inside a handler, Rebus sends the event together with the message's transaction: **if the handler fails, nothing is published**. An invalid order throws `BusinessValidationException` and fails fast to the error queue.

---

## Message contracts

Rebus identifies a message by its **.NET type name**, sent in the `rbs2-msg-type` header as `<full type name>, <assembly name>`, for example `Company.Product.Worker.Handlers.Models.OrderPlacedEvent, Company.Product.Worker`. The body is JSON (System.Text.Json).

What this means in practice:

- **A publisher has to send the same type name.** Another Rebus service gets it by referencing the same contract type, so share contracts as a small NuGet package. A non-Rebus publisher (a script, another language) must set `rbs2-msg-type`, `rbs2-content-type: application/json;charset=utf-8` and `rbs2-msg-id` itself, as `samples/requests.http` does.
- **Generating the project under another name changes the type string**, because the namespace and assembly name change. If the publisher uses the old name, the worker will not recognize its messages. Settle on a shared contracts package before you generate.
- **Treat contracts as a public API.** Add optional fields, never rename or remove them, and never rename or move a contract class without a plan for the messages already in the queues.
- Inbound contracts live in `Handlers/Models/`. Events the worker publishes live in `Accessors/Models/` (`...Entity`), because publishing is I/O.

---

## Switching brokers

The transport is the only broker-specific part. Everything above it (handlers, retries, error queue, pub/sub) behaves the same.

1. Add the transport package to `Directory.Packages.props` and the `.csproj` (for example `Rebus.AzureServiceBus`, `Rebus.AmazonSQS` or `Rebus.GoogleCloudPubSub`; check the package's README for its options).
2. Add a value to `MessagingTransport` in `Utilities/Options/MessagingOptions.cs`, and a case to `ConfigureTransport` in `Handlers/Setup/MessagingSetup.cs`:

   ```csharp
   case MessagingTransport.AzureServiceBus:
       transport.UseAzureServiceBus(options.ConnectionString, options.InputQueue);
       break;
   ```
3. Set `Messaging__Transport` and `Messaging__ConnectionString` for the environment.
4. Change the KEDA trigger in `deploy/kubernetes/worker.yaml` to the matching scaler (`azure-servicebus`, `aws-sqs-queue`, `gcp-pubsub`, ...).
5. Run `MessageHandlingTests` (they use the in-memory transport) and a manual smoke test against the real broker. How events are routed to subscribers differs by broker, so confirm the subscription works end to end.

Prefer a broker over a database-backed transport (`Rebus.PostgreSql`, `Rebus.SqlServer`): in this repo only the API touches a database.

---

## Adding a handler

Say you want to react to a `CustomerRegisteredEvent`:

1. **Contract:** `Handlers/Models/CustomerRegisteredEvent.cs` (no suffix beyond `Event`/`Command`).
2. **Use case:** a Manager method (plus Engines for logic and Accessors for I/O) in `Managers/`, with unit tests.
3. **Handler:** `Handlers/CustomerRegisteredHandler.cs`, implementing `IHandleMessages<CustomerRegisteredEvent>`. Keep it thin: call the Manager and decide the failure behaviour.
4. **Wire it** in `Handlers/Setup/HandlersSetup.cs`:

   ```csharp
   services.AddRebusHandler<CustomerRegisteredHandler>();
   // ...and in SubscribeToEventsAsync (events only; commands need no subscription):
   await bus.Subscribe<CustomerRegisteredEvent>();
   ```
5. **Try it:** add a request to `samples/requests.http`, and a case to `MessageHandlingTests`.

In Claude Code, `/add-handler <description>` walks through these steps.

---

## Reliability: retries, the error queue, idempotency

| Outcome | What the handler does | What Rebus does |
|---------|----------------------|-----------------|
| Success | returns | acknowledges; messages the handler published are sent now |
| **Transient** failure (API down, timeout) | lets the exception propagate | redelivers **immediately**, up to `MaxDeliveryAttempts` (5), then moves the message to the `error` queue |
| **Permanent** failure worth a look (`BusinessValidationException`) | lets it propagate | **fails fast**: moves it to the `error` queue after the first attempt |
| **Permanent** and harmless (unknown order) | catches, logs a warning, returns | acknowledges: the message is dropped |

A message in the error queue keeps its headers, including `rbs2-error-details` (the exception and attempts), so you can see why it failed. Alert on the error queue depth.

**Things to know:**

- **Retries have no delay.** Rebus's built-in retries happen back to back. The orders HTTP client retries with exponential backoff before an exception reaches the handler, which covers short outages. For a long outage, messages reach the error queue quickly: replay them when the dependency is back (RabbitMQ's shovel plugin or the management UI can move them to `worker`). Delayed retries need a timeout store or a broker with native delayed delivery; that is deliberately not in this template.
- **Delivery is at-least-once.** A message can arrive twice. Make handlers idempotent. The sample import isn't: replaying `ImportOrderCommand` creates the order again. For production, send an idempotency key (for example the command's `Reference`) to the API and let it ignore duplicates.
- **Publishing is atomic with the handler.** Events published from a handler are sent when it succeeds and discarded when it fails. This stops a half-finished retry from announcing something twice, but it is not a distributed transaction: if the API call succeeded and the handler fails afterwards, the retry calls the API again (see idempotency).
- **Graceful shutdown.** On SIGTERM, Rebus stops taking messages and waits up to `WorkerShutdownTimeout` (30s) for in-flight ones. Keep the pod's `terminationGracePeriodSeconds` higher (the manifest uses 60).

---

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `Messaging:Transport` | `RabbitMq` | Broker transport (`RabbitMq`, or `InMemory` for tests) |
| `Messaging:ConnectionString` | `amqp://guest:guest@localhost:5672` | Broker connection. Real credentials belong in a secret (`Messaging__ConnectionString`) |
| `Messaging:InputQueue` | `worker` | This worker's queue. Every replica reads it |
| `Messaging:ErrorQueue` | `error` | Where failed messages go |
| `Messaging:NumberOfWorkers` | `1` | Reader threads per replica |
| `Messaging:MaxParallelism` | `8` | Messages handled at once per replica |
| `Messaging:MaxDeliveryAttempts` | `5` | Attempts before the error queue |
| `Messaging:WorkerShutdownTimeout` | `00:00:30` | How long a stopping replica waits for in-flight messages |
| `ExternalApis:Orders` | `https+http://orders-api` | Orders API address, resolved by service discovery |
| `Services:orders-api:http:0` | `http://localhost:5080` in Development | Service discovery endpoint. In Kubernetes, leave it unset: the `orders-api` Service name resolves through DNS |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://localhost:4317` in Development | Telemetry collector |
| `Telemetry:PrometheusEndpoint:Enabled` | `true` | Serve `/metrics` |

Settings are validated when the worker starts: a missing queue name or a value below 1 stops it with a clear message.

---

## Deploying

The worker is a normal container (port 8080 for `/health` and `/metrics`, non-root user 1654). There is no sidecar.

### Kubernetes (any cloud: AKS, EKS, GKE, on-prem)

1. Install [KEDA](https://keda.sh): `helm install keda kedacore/keda -n keda --create-namespace`.
2. Edit `deploy/kubernetes/worker.yaml`: the image, the secret (use your secret manager in real life), and the OTLP endpoint.
3. `kubectl apply -n <namespace> -f deploy/kubernetes/worker.yaml`.

**Scaling like Functions:** the `ScaledObject` adds replicas when the `worker` queue backs up (20 messages per replica by default, up to 10 replicas) and removes them when it drains. KEDA has scalers for RabbitMQ, Azure Service Bus, SQS, Pub/Sub and Kafka.

**Scaling to zero:** set `minReplicaCount: 0` only **after the worker has run once**. Rebus creates the queue and the event subscriptions when a replica first starts. An event published before that has no queue to land in. After that the broker holds messages while no pod runs, and KEDA starts a pod when there is a backlog.

**Scheduled jobs:** deploy a separate single-replica scheduler app alongside this worker. It sends commands to this worker's queue.

### Azure Container Apps

Deploy the image and add a KEDA-style scale rule on the queue (Container Apps has them built in). There is nothing else to enable.

### Not supported: Azure App Service

App Service can run the container, but you would have to build the queue-based scaling yourself. Container Apps or Kubernetes is simpler.

---

## Telemetry

Logs, traces and metrics go through OpenTelemetry, exported over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, and scrapeable at `/metrics`. Rebus writes to the same logger, and every delivery attempt is a span with the orders API call as a child, so one trace shows the whole handling. Custom metrics:

| Metric | Meaning |
|--------|---------|
| `messages.handled` (`message.type`, `outcome`) | Delivery attempts. One message that fails twice and then succeeds counts as 2 `failure` and 1 `success` |
| `messages.duration` (`message.type`, `outcome`) | Time spent in the handler, in seconds |
| `orders.imported` | Orders created from `ImportOrderCommand` |

Trace context is not propagated from the publisher through the message headers, so a trace starts at the worker. The production setup (OpenTelemetry Collector with Grafana/Prometheus, Azure, AWS or GCP) is described in the API template's README, "Production telemetry setup", and applies unchanged. Also alert on **queue depth** and **error queue depth**, which the broker reports (RabbitMQ's Prometheus plugin, or your cloud's metrics).

---

## Testing

```bash
dotnet test                         # or: docker build .  (runs every test)
```

| Suite | Covers |
|-------|--------|
| `Engines/` | Notification text |
| `Managers/` | Order notification and import use cases |
| `Accessors/` | Orders API calls (stub HTTP handler), event publishing (substitute `IBus`) |
| `Handlers/` | The fail-fast rule for permanent failures |
| `Architecture/` | iDesign layering, no direct `HttpClient`, only Accessors use `IBus`/`IHttpClientFactory`, no I/O in Engines, model suffixes, handler naming, **no hosted services or timers** |
| `Host/` | Starts the worker in memory on Rebus's in-memory transport and sends real messages: handlers run, a missing order is dropped, an invalid order fails after one attempt, a transient failure gets exactly `MaxDeliveryAttempts`, metrics appear |

No broker is needed to run the tests.

---

## Rebus or Dapr?

This repo also has `worker-dapr`. Both give you handlers, retries and autoscaling; pick per project.

| | `worker-rebus` (this) | `worker-dapr` |
|---|---|---|
| Moving parts | Your process and the broker | Your process, a Dapr sidecar and component YAML |
| Switching brokers | Code: add a transport package and a `case` in one file | YAML only, no code change |
| Message model | Typed .NET messages, events and commands, `IHandleMessages<T>` | HTTP endpoints, CloudEvents |
| Publisher coupling | Publishers must send the same .NET type name (shared contracts) | Any HTTP client or Dapr SDK can publish |
| Retries | Built in: immediate, then the error queue; fail-fast rules in code | Resiliency YAML: exponential backoff, then dead-letter |
| File triggers | Not included (add an object-storage notification as a message) | Object storage and drop-folder functions included |
| Local debugging | Run the process, set a breakpoint in the handler | Needs the sidecar in Docker calling back to the host |
| .NET only? | Yes, on both ends (or hand-built headers) | Any language |

Choose Rebus when the services that talk to each other are all .NET and you want typed contracts, transactional publishing and fewer moving parts. Choose Dapr when you need to change brokers by configuration, mix languages, or use its other building blocks (bindings, state, secrets).

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Worker fails at start with `Messaging:...` in the message | A setting is missing or below 1 (see [Configuration](#configuration)) |
| Worker can't connect | RabbitMQ isn't running (`docker compose up -d --wait`), or `Messaging:ConnectionString` is wrong. In containers use host `rabbitmq`, not `localhost` |
| Published messages never reach the worker | Events: the worker must have started once, so its subscription exists. Check the exchange and the `worker` queue's bindings in the RabbitMQ UI. Commands: the routing key must be the queue name |
| Message lands in the `error` queue immediately | Look at `rbs2-error-details`. For `BusinessValidationException` that is by design (fail fast). For an error about an unknown or unresolvable type, the `rbs2-msg-type` header doesn't match a contract in this assembly: see [Message contracts](#message-contracts) |
| Every message fails with a `ResourceAccessException` | The orders API isn't reachable. Check `Services:orders-api` (or `ExternalApis:Orders`) and that the API runs |
| `OrderPlacedEvent` is logged as dropped | The order id doesn't exist in the orders API. Use an existing id |
| Messages pile up and nothing scales | KEDA isn't installed, the `ScaledObject` can't read the queue (check the `TriggerAuthentication` secret), or `queueName` doesn't match `Messaging__InputQueue` |
| Pod is killed while handling messages | `terminationGracePeriodSeconds` is lower than `WorkerShutdownTimeout`. Raise it |
