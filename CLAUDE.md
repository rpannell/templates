# coding-templates

A collection of `dotnet new` templates for .NET/C# services. Each folder under `templates/` is one template and is itself a working, buildable project.

## Layout

```
templates/
├── api-idesign/      dotnet new idesign-api   (Web API, iDesign layers, EF Core + Postgres)
├── worker-dapr/      dotnet new worker-dapr   (Functions-style worker: event and file triggers through Dapr, autoscaled)
├── worker-rebus/     dotnet new worker-rebus  (Functions-style worker: event and command handlers on Rebus, autoscaled)
├── scheduler-dapr/   dotnet new scheduler-dapr (cron jobs through Dapr cron bindings, exactly one replica)
└── db-liquibase/     dotnet new db-liquibase  (Liquibase XML changesets with rollbacks, migration Docker image)
CodingTemplates.csproj   packs every template into one NuGet template package
Dockerfile.verify        installs the pack, generates each template, builds and tests the output
```

Each template has its own `CLAUDE.md`. It is copied into every generated project, so it's written for the generated project, not for this repo. Keep template-specific rules there, not here.

## Rules for Working on Templates

- **Everything must compile inside Docker.** Do not assume a local SDK. Each template has a `Dockerfile` that builds and runs its tests. `Dockerfile.verify` proves the template works after `dotnet new` renames it.
- **The template source is a real project.** Edit it and build it in place. Don't use template-engine conditionals (`#if`) unless a template option truly needs them.
- **Placeholder names:** the `sourceName` in `.template.config/template.json` (for example `Company.Product`) gets replaced with the user's `-n` value in file names, namespaces and content. Use it consistently. Never hard-code a different product name.
- **Target framework:** `net10.0`, set once in each template's `Directory.Build.props`.
- **Package versions:** Central Package Management. Versions live only in each template's `Directory.Packages.props`.
- **Data ownership across templates:** only an API template talks to a database. Workers call the API. The Liquibase template owns every schema change, and no template uses EF Core migrations.
- **Scheduled work only in scheduler-dapr.** Never add cron bindings, timer loops or hosted services to a worker (it autoscales, and a schedule fires once per replica). worker-rebus has an architecture test for this.
- **Cloud-agnostic:** no vendor SDKs in application code. Brokers, storage and schedules go through Dapr components (worker-dapr), and telemetry goes through OTLP to an OpenTelemetry Collector. The platform choice lives in configuration files. worker-rebus names its broker in exactly one file (`Handlers/Setup/MessagingSetup.cs`), and that is the only place a Rebus transport package is used.
- Don't commit `bin/`, `obj/`, `.vs/` or `*.user` files.

## Verify

```bash
docker build -f Dockerfile.verify .                 # all templates, as generated
docker build templates/api-idesign                  # one template's source, build + test
docker build templates/db-liquibase                 # changelog checks + migration image
docker build templates/worker-dapr                  # worker source, build + test
docker build templates/worker-rebus                 # Rebus worker source, build + test
docker build templates/scheduler-dapr               # scheduler source, build + test
(cd templates/db-liquibase && docker compose --profile verify run --rm --build verify)   # rollback round trip
```

Install locally to try a template:

```bash
dotnet new install ./templates/api-idesign
dotnet new idesign-api -n Acme.Sales -o Acme.Sales
```
