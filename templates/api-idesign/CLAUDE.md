# iDesign Method Architecture

This service follows the iDesign Method. It is a single API project with five layers separated by folder and namespace. Every layer has a single responsibility, and dependencies only flow downward.

## Data Ownership

**This API is the only thing that talks to its database.** Workers, other services and UIs call this API's endpoints. They never get a connection string and never read the tables directly. If a new consumer needs data, add an endpoint here.

The schema is owned by Liquibase changesets, not EF Core migrations. Never add EF migrations. EF Core is used for queries only.

## Layers

| Layer | Namespace | Role | Injects | Constraints |
|-------|-----------|------|---------|-------------|
| **Client** (Controller) | `Controllers` | Thin HTTP endpoint. Maps API model to Dto and delegates to one Manager per action | Managers only | No business logic, no I/O |
| **Manager** | `Managers` | Orchestrates a use case. Sequences Engine and Accessor calls | Engines + Accessors | No calculations, no direct I/O, no other Managers |
| **Engine** | `Engines` | Pure business logic: calculations, validation, decisions | Other Engines or Utilities only | **No I/O of any kind** (no `HttpClient`, `DbContext`, `IDistributedCache`, file system) |
| **Accessor** | `Accessors` | All I/O: DB, HTTP, cache, files, queues | Utilities + I/O primitives | No business logic, no other Accessors |
| **Utility** | `Utilities` | Cross-cutting stateless helpers, extensions, constants | Nothing from above layers | Stateless, no layer references |

## Dependency Rules

```
Client -> Manager -> Engine    (pure logic, no I/O)
                  -> Accessor  (all I/O)

Utilities -> available to all layers
```

- No upward dependencies. No lateral calls within the same layer (no Manager -> Manager, no Accessor -> Accessor). Engine -> Engine is allowed.
- A Manager that needs another Manager's work does not call it directly. It publishes a message (Rebus) and the other workflow picks it up.
- **Interface-first**: every Manager, Engine, and Accessor has an `I{Name}` interface in the same file, above the class.

### Utility-Equivalent Framework Abstractions

These framework abstractions are stateless config, decision or cross-cutting helpers. They count as **Utilities**, so any layer (including Engines) may inject them, and doing so is not I/O:

- **Options pattern**: `IOptions<T>`, `IOptionsMonitor<T>`, `IOptionsSnapshot<T>`
- **Feature flags**: `IFeatureManager` (from `Microsoft.FeatureManagement`)
- **Logging**: `ILogger<T>`
- **Time**: `TimeProvider` (never call `DateTime.UtcNow` or `DateTimeOffset.UtcNow` directly, so time is testable)

Wrapping these in a custom Accessor adds ceremony without architectural benefit.

## Accessor Granularity

One Accessor per **business resource**, not one per table and not one for the whole database. `OrderAccessor` and `CustomerAccessor` both use the same `AppDbContext`. Each external API gets its own Accessor under `Accessors/Http/` (for example `InventoryApiAccessor`).

Accessors expose business verbs (`SaveAsync`, `FindAsync`, `ListAsync`), not generic repository methods (`Query<T>`, `GetAll`).

## Database Access: Always Through the Factory

- Register the context only with `AddDbContextFactory<AppDbContext>`. Never use `AddDbContext`.
- Accessors inject `IDbContextFactory<AppDbContext>` and create one short-lived context per method: `await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);`
- **Never inject `DbContext` or `AppDbContext`**, anywhere. `DbContext_is_never_injected_directly` fails the build if you do.

## Calling Other APIs: IHttpClientFactory + Service Discovery

- Every outbound HTTP call goes through an HTTP Accessor in `Accessors/Http/`, one per external service.
- HTTP Accessors inject **`IHttpClientFactory`** and call `CreateClient(HttpClientName)` per request, without disposing the client. **Never inject `HttpClient`** or use typed clients: they are transient, and a Singleton would capture one forever. `HttpClient_is_never_injected_directly` enforces this.
- Register a **named client** in `AccessorsSetup` whose base address is a **service-discovery name**, read from configuration with a default: `https+http://inventory`. `https+http` means prefer HTTPS and fall back to HTTP.
- `AccessorsSetup` applies `AddServiceDiscovery()` and `AddStandardResilienceHandler()` (retry, circuit breaker, timeouts) to every client through `ConfigureHttpClientDefaults`. Don't add ad-hoc retry loops.
- Endpoints resolve from configuration first (`Services:{name}:{scheme}` array, for example `Services:inventory:http:0`). Otherwise the name passes through to DNS, which is a Kubernetes Service name in the cluster. Never hard-code hosts or ports in code.
- Only Accessors may inject `IHttpClientFactory` or `IDbContextFactory<T>` (`Only_accessors_use_database_or_http_factories`).
- Wrap `HttpRequestException`, Polly `ExecutionRejectedException`, timeouts and `JsonException` in `ResourceAccessException`, but let the caller's cancellation propagate.
- The external API's JSON shapes are `...Entity` types in `Accessors/Models/`. Accessor methods take and return Dtos.

## Logging and Telemetry (OpenTelemetry)

`Utilities/Setup/TelemetrySetup.AddTelemetry()` sends **ILogger logs, traces and metrics** through OpenTelemetry. They're exported over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Every log written during a request carries that request's trace and span IDs.

- Log through an injected `ILogger<T>` only. Never use `Console.WriteLine`, and don't add another logging framework.
- Use message templates with named placeholders: `logger.LogInformation("Created order {OrderId}", id)`. Never use string interpolation or concatenation in log calls, because they break structured fields.
- Never log secrets, tokens, connection strings or personal data. Log identifiers instead.
- Levels: `Information` for business events (order created), `Warning` for handled problems, `Error` with the exception for failures. Unhandled exceptions are already logged by ASP.NET Core, and `ApiExceptionHandler` logs 503s. Don't log and rethrow.
- Incoming requests, outgoing HTTP calls (`HttpClient`), database commands (Npgsql) and runtime/EF/Polly metrics are instrumented automatically. Don't add manual spans for these.
- Custom spans and metrics go through `Utilities/Telemetry/ApplicationTelemetry` (`ActivitySource`, `Meter`). Metric names are lowercase and dotted (`orders.created`), with a unit.
- **Cloud-agnostic by design:** the API only speaks OTLP to an OpenTelemetry Collector. The Collector config (`observability/otel-collector.yaml`) decides the backends. Locally, metrics go to Prometheus and Grafana, and logs and traces go to the Aspire dashboard. Never add a vendor-specific exporter or SDK (Azure Monitor, CloudWatch, Datadog...) to the API. Route to it from the Collector instead.
- **Metrics are exposed both ways:** OTLP push to the Collector, and Prometheus pull at `GET /metrics` (on by default, turned off with `Telemetry:PrometheusEndpoint:Enabled=false`). Keep both working. `MetricsEndpointTests` checks `/metrics`. Never remove the scrape endpoint or put it behind auth that stops Prometheus from scraping; keep it off the public ingress instead.
- Per-platform Collector setups (standard Grafana/Prometheus/Loki/Tempo, Azure, AWS, GCP) are in the README under "Production telemetry setup". Platform choice is Collector config only.
- Metrics reach Prometheus with OTLP-to-Prometheus names: `http.server.request.duration` becomes `http_server_request_duration_seconds`, `orders.created` becomes `orders_created_total`, and attributes become labels with underscores (`http_route`). `job` is the service name. When you add a custom metric, add a panel for it to `observability/grafana/dashboards/api-overview.json`.
- Configuration uses the standard `OTEL_*` settings (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_EXPORTER_OTLP_HEADERS`, `OTEL_RESOURCE_ATTRIBUTES`). Never hard-code a collector address.
- Console logs are JSON (with scopes) outside Development, for container log collectors.

## Model Naming

| Tier | Suffix | Location | Example | Used by |
|------|--------|----------|---------|---------|
| API contract | *None* | `Models/` | `Order`, `CreateOrderRequest` | Controllers only |
| Internal | `Dto` | `Managers/Models/` | `OrderDto` | Controllers, Managers, Engines, Accessors |
| I/O shape | `Entity` | `Accessors/Models/` | `OrderEntity` | Accessors only |

Mapping is explicit and lives at the boundary that owns it:

- API model <-> Dto: `Controllers/Mapping/`
- Dto <-> Entity: `Accessors/Mapping/` (Accessor methods take and return Dtos, so Entities never leave the Accessors namespace)

Never leak `Dto` or `Entity` types through API contracts. Never expose API models below the Controllers.

## Folder Structure

```
Company.Product.Api/
├── Controllers/              # Clients
│   ├── Mapping/              # API model <-> Dto
│   └── ErrorHandling/        # Exception -> ProblemDetails
├── Managers/                 # Managers
│   ├── Models/               # Dtos
│   ├── Exceptions/           # NotFoundException, BusinessValidationException
│   └── Setup/                # AddManagers()
├── Engines/                  # Engines
│   ├── Options/              # Options classes bound from configuration
│   └── Setup/                # AddEngines()
├── Accessors/                # Accessors
│   ├── Http/                 # HTTP accessors for other APIs (IHttpClientFactory)
│   ├── Database/             # AppDbContext
│   ├── Mapping/              # Dto <-> Entity
│   ├── Models/               # Entities (tables and external API shapes)
│   ├── Exceptions/           # ResourceAccessException
│   └── Setup/                # AddAccessors()
├── Models/                   # API contracts
├── Utilities/                # Utilities
│   ├── Telemetry/            # ApplicationTelemetry: ActivitySource + Meter
│   └── Setup/                # AddUtilities(), AddTelemetry()
└── Program.cs
```

## DI Registration

Each layer owns a `Setup/` folder with an `IServiceCollection` extension method. Wire them in `Program.cs`:

```csharp
builder.AddTelemetry();

builder.Services
    .AddUtilities()
    .AddAccessors(builder.Configuration)
    .AddEngines(builder.Configuration)
    .AddManagers();
```

Use **Singleton** lifetime by default, with `IDbContextFactory<AppDbContext>` in Accessors. Never inject `DbContext` directly into a Singleton. Create one context per Accessor method call with `await using var db = await dbFactory.CreateDbContextAsync(ct);`.

## Zero Warnings

The build has **zero warnings**. `TreatWarningsAsErrors` is on for compiler, analyzer, NuGet (including vulnerability audit) and MSBuild warnings, and the Dockerfile fails on lint warnings (`# check=error=true`).

- Fix the cause. Don't add `#pragma warning disable`, `NoWarn` or `[SuppressMessage]` unless there is no fix, and then add a comment on the same line explaining why.
- If a transitive package has a vulnerability, pin a fixed version under "Security pins" in `Directory.Packages.props`. Don't suppress the NU19xx warning.
- New packages go in `Directory.Packages.props`. Keep `nuget.config` to a single source.

## XML Documentation

XML docs are **required on every public type and member**. The build treats CS1591 (missing comment), CS1573 (missing `<param>`), CS1572 (unknown `<param>`) and CS1574/CS1734 (broken `cref`/`paramref`) as errors.

- Interfaces carry the full docs. Implementations use `/// <inheritdoc />`.
- Primary constructors and positional records document their parameters with `<param>` on the type.
- Document private and internal helpers too, when the intent isn't obvious from the name.
- Document thrown exceptions with `<exception cref="...">` on interface methods.

**Controllers need more than that.** The comments feed the OpenAPI document, so they're what API consumers read in Scalar. Every controller needs a `<summary>`. Every action needs:

- `<summary>`: one line, shown as the operation title
- `<remarks>`: optional detail, shown under the title
- `<param>` for every parameter, including `cancellationToken`
- `<returns>`
- `<response code="...">` for every `[ProducesResponseType]` on the action

Every action declares `[ProducesResponseType]` for each status code it can return, including 400/404/503 from `ApiExceptionHandler`. `ControllerDocumentationTests` fails the build if any of this is missing.

## API Reference UI

The built-in `Microsoft.AspNetCore.OpenApi` package generates the OpenAPI document at `/openapi/v1.json`. **Scalar** renders it at `/scalar`, and `/` redirects there. Swashbuckle/Swagger UI is not used. Don't add it.

Docs are on in Development. To turn them on elsewhere, set `ApiDocs__Enabled=true`.

## Errors

- Managers and Engines throw exceptions from `Managers/Exceptions/` (`NotFoundException` becomes 404, `BusinessValidationException` becomes 400).
- Accessors wrap infrastructure failures in `ResourceAccessException` (503).
- `Controllers/ErrorHandling/ApiExceptionHandler` turns them into ProblemDetails. Controllers never contain try/catch.

## Adding a Feature

1. API model in `Models/` (no suffix)
2. Dto in `Managers/Models/` (`Dto` suffix)
3. Entity in `Accessors/Models/` (`Entity` suffix), if new I/O is needed. Add the Liquibase changeset in the database repo too.
4. Accessor: implement the I/O method
5. Engine: implement the pure business logic
6. Manager: orchestrate Engine + Accessor
7. Controller action: inject only the Manager. Add `[ProducesResponseType]` for every status code and full XML docs.
8. Register new components in the matching `Setup/` method
9. Add unit tests for the Engine and Manager

Create a **new Engine** when the logic is a distinct responsibility. **Extend** an existing Engine when adding to an existing responsibility.

## Architecture Enforcement

`tests/Company.Product.Api.Tests/Architecture/` enforces these rules, and the Docker build runs the tests, so a violation fails the build. If a test fails, fix the code, not the test. The tests check:

| Test | Rule |
|------|------|
| `Controllers_inject_only_managers` | Controller constructors take only Manager interfaces |
| `Managers_inject_only_engines_accessors_and_utilities` | No Manager -> Manager, no framework I/O types |
| `Engines_inject_only_engines_and_utilities` | No Accessors, no I/O types |
| `Accessors_do_not_inject_other_layers` | No Accessor -> Accessor, Engine or Manager |
| `DbContext_is_never_injected_directly` | Nothing injects a `DbContext`. Use `IDbContextFactory<T>` |
| `HttpClient_is_never_injected_directly` | Nothing injects `HttpClient`. Use `IHttpClientFactory` |
| `Only_accessors_use_database_or_http_factories` | `IDbContextFactory<T>` and `IHttpClientFactory` are injected only in Accessors |
| `Engines_do_no_io` | Engine code has no dependency on EF Core, `System.Net.Http`, `System.IO`, caching or Accessors |
| `Utilities_do_not_depend_on_layers` | Utilities never reference Controllers, Managers, Engines or Accessors |
| `Entities_stay_inside_accessors` | Only Accessors reference `Accessors.Models` |
| `Api_models_stay_in_controllers` | Only Controllers reference `Models` |
| `Components_have_matching_interface` | Every Manager, Engine and Accessor implements `I{Name}` |
| `Model_suffixes_match_tier` | `Dto` in `Managers.Models`, `Entity` in `Accessors.Models`, neither in `Models` |
| `Controllers_and_actions_have_complete_xml_docs` | Summary, params, returns and a `<response>` per `[ProducesResponseType]` on every action |

**For any class, verify:**

1. **What does it inject?** It must match the layer's allowed dependencies.
2. **Does it do I/O?** Only Accessors should.
3. **Is the model suffix correct?** Controller -> no suffix, Manager/Engine -> `Dto`, Accessor -> `Entity`.
4. **Does it have an `I{Name}` interface?** It's required for every Manager, Engine, and Accessor.

## Build and Test

```bash
dotnet build
dotnet test
docker build -t company-product-api .     # builds, runs all tests, produces the runtime image
docker compose up --build                 # API on http://localhost:8080 (Scalar at /scalar) with Postgres
```

In VS Code, press F5 and pick **API + Postgres + Telemetry (Docker)**. It starts Postgres, the OpenTelemetry Collector, Prometheus, Grafana and the Aspire dashboard, builds, runs the API on http://localhost:5080 and opens Scalar. Grafana (metrics) is at http://localhost:3000, and the Aspire dashboard (logs and traces) is at http://localhost:18888. Tasks (`Ctrl+Shift+P` > Run Task) cover `test`, `db: reset` and `docker: build + test`.
