# coding-templates

`dotnet new` templates for .NET services. Everything builds and tests inside Docker.

| Template | Short name | Status |
|----------|------------|--------|
| [iDesign Web API](templates/api-idesign) | `idesign-api` | Ready |
| [Dapr worker](templates/worker-dapr) (Functions-style: event and file triggers, autoscaled) | `worker-dapr` | Ready |
| [Rebus worker](templates/worker-rebus) (Functions-style: event and command handlers on RabbitMQ or any Rebus transport, autoscaled with KEDA) | `worker-rebus` | Ready (new, see its README) |
| [Dapr scheduler](templates/scheduler-dapr) (cron jobs, exactly one replica) | `scheduler-dapr` | Ready |
| [Liquibase database](templates/db-liquibase) (XML changesets with rollbacks, migration image) | `db-liquibase` | Ready |

## Install

```bash
dotnet new install ./templates/api-idesign        # from a clone
# or
dotnet pack CodingTemplates.csproj -o ./packages
dotnet new install ./packages/CodingTemplates.1.0.0.nupkg
```

## Use

```bash
dotnet new idesign-api -n Acme.Sales -o Acme.Sales
cd Acme.Sales
docker compose up --build        # http://localhost:8080/scalar

dotnet new db-liquibase -n Acme.Sales.Database -o Acme.Sales.Database
cd Acme.Sales.Database
docker compose up -d --wait db && docker compose run --rm --build migrate

dotnet new worker-dapr -n Acme.Sales.Worker -o Acme.Sales.Worker
cd Acme.Sales.Worker
docker compose --profile container up --build   # worker + Dapr sidecar + RabbitMQ

dotnet new worker-rebus -n Acme.Sales.RebusWorker -o Acme.Sales.RebusWorker
cd Acme.Sales.RebusWorker
docker compose --profile container up --build   # worker + RabbitMQ

dotnet new scheduler-dapr -n Acme.Sales.Scheduler -o Acme.Sales.Scheduler
cd Acme.Sales.Scheduler
docker compose --profile container up --build   # scheduler + Dapr sidecar (cron)
```

## Run from VS Code

Open the repo folder and press **F5**, then choose **API + Postgres + Telemetry (Docker)**. It starts Postgres and the open-source telemetry stack in Docker, builds the template source, runs the API on http://localhost:5080 and opens Scalar. Metric dashboards are in Grafana at http://localhost:3000, and logs and traces are at http://localhost:18888. It needs the .NET 10 SDK, Docker Desktop and the C# Dev Kit extension (VS Code will offer the recommended extensions).

## Build everything

Before pushing, build and test every template exactly like CI:

```powershell
./build.ps1              # Windows (PowerShell): .NET build + tests, template pack, Docker images, Liquibase round trip, generated-template check
./build.ps1 -SkipDocker  # just the .NET steps
```

```bash
./build.sh               # Linux/macOS
./build.sh --skip-docker
```

It prints a PASS/FAIL summary and writes the full log to `artifacts/build.log`. Warnings are errors, so a PASS also means zero warnings.

## Verify

```bash
docker build templates/api-idesign              # template source: build + tests
docker build -f Dockerfile.verify .             # every template, as generated
docker build templates/db-liquibase             # changelog checks + migration image
docker build templates/worker-dapr              # worker source: build + tests
docker build templates/worker-rebus             # Rebus worker source: build + tests
docker build templates/scheduler-dapr           # scheduler source: build + tests
```

## iDesign Web API

A single ASP.NET Core project with iDesign layers as folders:

- **Controllers**: thin HTTP clients of Managers
- **Managers**: use-case orchestration
- **Engines**: pure business logic, no I/O
- **Accessors**: all I/O (EF Core + Postgres)
- **Utilities**: cross-cutting helpers

Architecture tests enforce the layering, and the XML-doc tests enforce controller docs, so a violation fails the Docker build. The API is the only component that talks to its database. The OpenAPI document is rendered with Scalar. The rules Claude follows in generated projects are in [`templates/api-idesign/CLAUDE.md`](templates/api-idesign/CLAUDE.md).
