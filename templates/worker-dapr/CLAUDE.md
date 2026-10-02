# Company.Product.Worker: Functions-Style Worker on Dapr

A cloud-agnostic replacement for Azure Functions. **Functions** are small classes that run when something happens: an **event** arrives or a **file** lands. The [Dapr](https://dapr.io) sidecar connects the functions to brokers and storage, and turns each trigger into an HTTP call to the worker. Which broker or store is used is decided by the YAML files in `dapr/components/`, **never by code**.

## Triggers

| Function | Trigger | Dapr building block | Local | Cloud options (component swap) |
|----------|---------|---------------------|-------|--------------------------------|
| `OrderPlacedFunction` | **Event**: topic `order-placed` | pub/sub component `events` | RabbitMQ | Kafka, Azure Event Hubs, Azure Service Bus, AWS SNS/SQS, GCP Pub/Sub |
| `ObjectCreatedFunction` | **File** created in object storage | input binding `object-created`, plus output binding `object-storage` to read it | RabbitMQ queue + local files | S3 -> SQS, Blob -> Event Grid -> Service Bus, GCS -> Pub/Sub |
| `DropFolderFunction` | **File** dropped in a folder | none: a hosted service polling a mounted folder | `./drop` | any mounted volume: NFS, SMB, Azure Files, EFS, Filestore, SFTP landing folder |

## Layers (iDesign)

Same rules as the API template:

| Layer | Namespace | Role | May inject |
|-------|-----------|------|------------|
| **Function** (client) | `Functions` | Receives the trigger, calls **one** Manager method, maps the outcome to a Dapr response | Managers, utility-equivalents |
| **Manager** | `Managers` | Orchestrates the use case | Engines, Accessors |
| **Engine** | `Engines` | Pure logic: parsing, validation, calculations, message text. **No I/O** | Engines, utility-equivalents |
| **Accessor** | `Accessors/<Kind>/` | All I/O: the orders API (`Http/`), object storage through Dapr (`Storage/`), the drop folder (`Files/`), notifications (`Notifications/`) | Utilities, I/O primitives (`IHttpClientFactory`, `DaprClient`) |
| **Utility** | `Utilities` | Options classes, telemetry, setup | nothing above |

- Utility-equivalents that may be injected anywhere: `IOptions<T>`/`IOptionsMonitor<T>`/`IOptionsSnapshot<T>`, `IFeatureManager`, `ILogger<T>`, `TimeProvider`.
- Every Manager, Engine and Accessor has an `I{Name}` interface in the same file. Everything is a Singleton.
- Models: function contracts (event payloads) in `Functions/Models/` with no suffix. Internal models in `Managers/Models/` with the `Dto` suffix. External JSON shapes (API requests and responses) in `Accessors/Models/` with the `Entity` suffix, and they never leave Accessors.
- **The worker never touches a database.** It reads and writes business data through the API (`OrdersApiAccessor`), which is found by service discovery (`https+http://orders-api`).
- Only Accessors inject `DaprClient` or `IHttpClientFactory`. Never inject `HttpClient`. HTTP clients are named clients with `AddStandardResilienceHandler()` and `AddServiceDiscovery()`, registered in `AccessorsSetup`.

## Writing a Function

1. **Idempotent.** Every trigger is at-least-once: a message can arrive twice, a file can be imported again after a crash. Design the use case so a repeat is harmless (check before creating, or pass an idempotency key to the API).
2. **Return the right outcome:**
   - Pub/sub: success is `PubSubResults.Success()`. **Transient** failures (`ResourceAccessException`, anything unexpected) throw, which becomes a 500, and Dapr retries per `dapr/components/resiliency.yaml`, then dead-letters. **Permanent** failures (`NotFoundException`, `BusinessValidationException`) return `PubSubResults.Drop()`.
   - Input bindings: 200 acknowledges the message. A 500 makes the broker redeliver it. Acknowledge permanent failures (log them) so they don't loop forever.
   - Drop folder: permanent failures go to `failed/` with an `.error.txt`. Transient ones go back to `incoming/` and are retried on the next poll.
3. **Thin:** no business logic in a Function. It reads the trigger, calls one Manager method, and maps exceptions to outcomes.
4. **Wire it up** in `Functions/Setup/FunctionsSetup.cs`:
   - Event: `app.MapPost(Route, ...).WithTopic(PubSubName, Topic)`. If the publisher isn't Dapr (a raw RabbitMQ or Kafka message), use the `WithTopic` overload with raw payload turned on.
   - Binding (queue, storage notification): `MapBinding(app, Route, ...)`, plus a component in `dapr/components/` with `route: <Route>`.
   - Register the Function class as a Singleton in `AddFunctions`.
5. **Names:** routes are `/functions/<kebab-name>`. Component names describe the trigger (`events`, `object-created`), not the technology.
6. **Tests:** Engine and Manager unit tests. `HostTests` checks that `/dapr/subscribe` and the binding routes still line up.

## No scheduled work here

**Never add a cron binding or a timer loop to this worker.** It autoscales (0..N replicas), and a schedule fires once per replica. Scheduled jobs belong in the `scheduler-dapr` template, which runs as exactly one replica. If a job needs to fan work out, the scheduler can publish events that this worker consumes, and those scale normally.

## Logging, telemetry and the zero-warning build

These work the same as in the API template:

- **Logging:** use structured `ILogger<T>` message templates with no interpolation, and never log secrets or personal data.
- **Telemetry:** logs, traces and metrics go through OpenTelemetry (`TelemetrySetup`), and are exported over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. `/metrics` is also exposed for Prometheus. Custom metrics use `ApplicationTelemetry.Meter` (`files.imported`, `orders.imported`).
- **Build rules:** XML docs are required (CS1591 is an error), warnings are errors, versions live in `Directory.Packages.props`, and the architecture tests run in the Docker build.
- **Package choices:** no vendor SDKs (Azure, AWS or GCP clients) in the worker. Reach cloud services through Dapr components or HTTP.

## Build and run

```bash
dotnet build && dotnet test
docker build -t company-product-worker .            # compile + all tests + runtime image
docker compose --profile debug up -d --wait         # RabbitMQ, dashboard, Dapr sidecar for F5 (worker on :5100)
docker compose --profile container up --build       # everything in containers
```

`samples/requests.http` triggers each function by hand through the sidecar (`http://localhost:3500`).
