---
description: Add a scheduled job (cron) following the scheduler's iDesign and Dapr rules
argument-hint: <what the job does, and how often>
---

Add this scheduled job following CLAUDE.md: $ARGUMENTS

1. Build the use case bottom-up: Accessor(s) for I/O (other APIs through `IHttpClientFactory` named clients with service discovery), Engine(s) for pure logic, then one Manager method. XML docs on every public member.
2. Create `Jobs/<Name>Job.cs` following `DailySalesSummaryJob`: `Name` (kebab-case, equal to the Dapr component name), `Route` (`/jobs/<name>`), inject the Manager and `JobRunGuard`, run through `guard.RunAsync(Name, ...)`, return `JobResults.From(Name, outcome)`.
3. Register and map it in `Jobs/Setup/JobsSetup.cs` (`AddSingleton` + `MapScheduledJob`).
4. Add the schedule twice: `dapr/components/<name>.yaml` (a frequent schedule for local development) and `deploy/kubernetes/components.yaml` (the real schedule, in UTC, 6 fields with seconds, with `scopes: [scheduler]`).
5. Make the job idempotent and safe to skip: a missed run is not caught up, and a run can repeat after a crash.
6. If the work is large, publish events for the worker-dapr template to process instead of doing it all here.
7. Add unit tests for the Engine and Manager, add the route to `HostTests`, and add a request to `samples/requests.http`.
8. Run `dotnet build` and `dotnet test`. Architecture tests (including "every job uses JobRunGuard") must pass, with zero warnings.
