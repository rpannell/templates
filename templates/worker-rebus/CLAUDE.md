# Company.Product.Worker: Message Worker on Rebus

A cloud-agnostic replacement for Azure Functions with a queue or topic trigger. **Handlers** are small classes that run when a **message** arrives: an **event** (something happened, many possible subscribers) or a **command** (do this, sent to this worker's queue). [Rebus](https://github.com/rebus-org/Rebus) reads the worker's queue and calls the handlers. The broker is RabbitMQ locally; production uses whichever Rebus transport you pick, chosen in **one file** (`Handlers/Setup/MessagingSetup.cs`) plus configuration.

## Messages

| Message | Kind | Handler | How it gets here |
|---------|------|---------|------------------|
| `OrderPlacedEvent` | **Event** | `OrderPlacedHandler` | Published by another service. The worker subscribes in `HandlersSetup.SubscribeToEventsAsync` |
| `ImportOrderCommand` | **Command** | `ImportOrderHandler` | Sent to the worker's input queue (`Messaging:InputQueue`, default `worker`) |

The worker also **publishes** `OrderImported` (from `OrderEventsAccessor`) after a successful import.

## Layers (iDesign)

Same rules as the API template:

| Layer | Namespace | Role | May inject |
|-------|-----------|------|------------|
| **Handler** (client) | `Handlers` | Receives the message, calls **one** Manager method | Managers, utility-equivalents |
| **Manager** | `Managers` | Orchestrates the use case | Engines, Accessors |
| **Engine** | `Engines` | Pure logic: validation, calculations, message text. **No I/O** | Engines, utility-equivalents |
| **Accessor** | `Accessors/<Kind>/` | All I/O: the orders API (`Http/`), publishing to the bus (`Messaging/`), notifications (`Notifications/`) | Utilities, I/O primitives (`IHttpClientFactory`, `IBus`) |
| **Utility** | `Utilities` | Options classes, telemetry (including the Rebus pipeline step), setup | nothing above |

- Utility-equivalents that may be injected anywhere: `IOptions<T>`/`IOptionsMonitor<T>`/`IOptionsSnapshot<T>`, `IFeatureManager`, `ILogger<T>`, `TimeProvider`.
- Every Manager, Engine and Accessor has an `I{Name}` interface in the same file. Everything is a Singleton. Handlers are the exception: Rebus creates one per message.
- Models: **inbound** message contracts in `Handlers/Models/` with no suffix (`...Event`, `...Command`). Internal models in `Managers/Models/` with the `Dto` suffix. External shapes (API JSON, **outbound** events) in `Accessors/Models/` with the `Entity` suffix, and they never leave Accessors.
- **The worker never touches a database.** It reads and writes business data through the API (`OrdersApiAccessor`), found by service discovery (`https+http://orders-api`).
- Only Accessors inject `IBus` or `IHttpClientFactory`. Never inject `HttpClient`. HTTP clients are named clients with `AddStandardResilienceHandler()` and `AddServiceDiscovery()`, registered in `AccessorsSetup`.
- Handlers have no `CancellationToken` (Rebus doesn't pass one), so they pass `CancellationToken.None`. On shutdown Rebus waits for in-flight messages (`Messaging:WorkerShutdownTimeout`).

## Writing a Handler

1. **Idempotent.** Delivery is at-least-once: a message can arrive twice (a crash after the work but before the acknowledgment, or a redelivery). Design the use case so a repeat is harmless (check before creating, or pass an idempotency key to the API).
2. **Pick the failure behaviour:**
   - **Transient** (`ResourceAccessException`, anything unexpected): let it propagate. Rebus redelivers it immediately, up to `Messaging:MaxDeliveryAttempts` (default 5), then moves it to the error queue.
   - **Permanent, but worth a look** (`BusinessValidationException`): let it propagate. `PermanentFailureFailFastChecker` sends it to the error queue after the first attempt.
   - **Permanent and harmless** (`NotFoundException`, an event about something that no longer exists): catch it in the handler, log a warning, and return. The message is acknowledged and dropped.
3. **Thin:** no business logic in a Handler. It reads the message, calls one Manager method, and decides the failure behaviour.
4. **Wire it up** in `Handlers/Setup/HandlersSetup.cs`: `services.AddRebusHandler<MyHandler>()`. For an **event**, also add `await bus.Subscribe<MyEvent>()` to `SubscribeToEventsAsync`. Commands need no subscription.
5. **Message contracts:** Rebus identifies a message by its .NET type name (the `rbs2-msg-type` header: `<full type name>, <assembly name>`). A publisher must send the same type name. Share contracts as a small NuGet package, or accept that renaming the project renames the type string. Never rename or move a contract class without a plan for in-flight messages.
6. **Tests:** Engine and Manager unit tests. Add a `MessageHandlingTests` case for any routing or retry behaviour you rely on: it runs the real Rebus pipeline on the in-memory transport.

## No scheduled work here

**Never add a timer loop, `BackgroundService`, `PeriodicTimer` or cron-like trigger to this worker.** It autoscales (KEDA, 1..N replicas), and a timer would fire once per replica. An architecture test fails the build if the worker contains a hosted service. Scheduled jobs belong in a separate single-replica scheduler app. If a job needs to fan work out, the scheduler can send commands that this worker consumes, and those scale normally.

## Logging, telemetry and the zero-warning build

- **Logging:** use structured `ILogger<T>` message templates with no interpolation, and never log secrets or personal data. Rebus logs through the same `ILoggerFactory`.
- **Telemetry:** logs, traces and metrics go through OpenTelemetry (`TelemetrySetup`), exported over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. `/metrics` is also exposed for Prometheus. `MessageTelemetryStep` records `messages.handled` and `messages.duration` (`message.type`, `outcome`) per delivery attempt. Custom metrics use `ApplicationTelemetry.Meter` (`orders.imported`).
- **Build rules:** XML docs are required (CS1591 is an error), warnings are errors, versions live in `Directory.Packages.props`, and the architecture tests run in the Docker build.
- **Package choices:** the Rebus transport package is the only broker dependency, and it is referenced only from `MessagingSetup`. No other vendor SDKs (Azure, AWS or GCP clients) in the worker: reach cloud services over HTTP.

## Build and run

```bash
dotnet build && dotnet test
docker build -t company-product-worker .            # compile + all tests + runtime image
docker compose up -d --wait                         # RabbitMQ + dashboard for F5 (worker on :5100)
docker compose --profile container up --build       # everything in containers
```

`samples/requests.http` sends each message to the worker through RabbitMQ's management API.
