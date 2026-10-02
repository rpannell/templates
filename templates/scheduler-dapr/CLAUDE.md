# Company.Product.Scheduler: Scheduled Jobs on Dapr

This app runs **scheduled jobs** (cron). A [Dapr](https://dapr.io) sidecar holds the schedules as `bindings.cron` components and calls `POST /jobs/<name>` whenever one fires. The app doesn't reference Dapr; it only serves those endpoints.

It's the companion of the `worker-dapr` template. The worker autoscales on events and files. **The scheduler runs as exactly one replica**, because every replica's sidecar fires every schedule.

## Non-negotiable rules

1. **One replica, always.** Deployment `replicas: 1`, `strategy: Recreate`, and no HPA or KEDA. Two pods means every job runs twice.
2. **Every job runs through `JobRunGuard`**, which skips overlapping runs and writes logs and metrics. An architecture test enforces this.
3. **Jobs are idempotent and skip-safe.** A run can repeat (sidecar restart) or be missed (pod down at the tick, with no catch-up). Design the work so either is harmless.
4. **Schedules are UTC**, 6 fields with seconds (`0 0 6 * * *` = 06:00:00 UTC). Convert local times yourself and remember daylight saving.
5. **Schedules live in YAML, never in code:** `dapr/components/*.yaml` (local, frequent) and `deploy/kubernetes/components.yaml` (real, with `scopes: [scheduler]`). The component name equals the job's `Name`, and its `route` equals the job's `Route`.
6. **Keep runs short** (seconds to a few minutes). The sidecar's HTTP call stays open for the whole run. For big workloads, have the job publish events and let the `worker-dapr` template process them in parallel.
7. **No database access.** Read and write through APIs (`Accessors/Http`, `IHttpClientFactory` named clients with service discovery and standard resilience).
8. **Job endpoints are internal.** Never route `/jobs/*` through a public ingress.

## Layers (iDesign)

| Layer | Namespace | Role | May inject |
|-------|-----------|------|------------|
| **Job** (client) | `Jobs` | `Name` + `Route` constants. Runs one Manager call through `JobRunGuard` and returns `JobResults.From(...)` | Managers, `JobRunGuard`, utility-equivalents |
| **Manager** | `Managers` | Orchestrates the use case | Engines, Accessors |
| **Engine** | `Engines` | Pure logic (calculations, message text). No I/O | Engines, utility-equivalents |
| **Accessor** | `Accessors/<Kind>/` | All I/O: other APIs (`Http/`), notifications (`Notifications/`) | Utilities, `IHttpClientFactory` |
| **Utility** | `Utilities` | Telemetry, setup, `Scheduling/JobRunGuard` (the one stateful Utility: a lock per job) | nothing above |

- Utility-equivalents: `IOptions*`, `IFeatureManager`, `ILogger<T>`, `TimeProvider`. Use `TimeProvider` for "now", never `DateTime.UtcNow`.
- Every Manager, Engine and Accessor has an `I{Name}` interface in the same file. Everything is a Singleton.
- Models: `Dto` suffix in `Managers/Models`, `Entity` suffix in `Accessors/Models` (external JSON shapes, never leaving Accessors).
- XML docs are required (CS1591 is an error), warnings are errors, and package versions live in `Directory.Packages.props`.

## Adding a job

Follow `/add-job` (`.claude/commands/add-job.md`), or the "Adding a job" section of README.md. In short: Manager (+ Engine/Accessor), then `Jobs/<Name>Job.cs`, then register and map it in `JobsSetup`, then add its component to both `dapr/components/` and `deploy/kubernetes/components.yaml`, then tests, then a request in `samples/requests.http`.

## Build and run

```bash
dotnet build && dotnet test
docker build -t company-product-scheduler .          # compile + all tests + runtime image
docker compose --profile debug up -d --wait          # dashboard + Dapr sidecar for F5 (scheduler on :5200)
docker compose --profile container up --build        # everything in containers (scheduler on :8081)
```
