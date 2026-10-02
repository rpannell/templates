---
description: Add a function (event or file trigger) following the worker's iDesign and Dapr rules
argument-hint: <what should happen, and on which trigger>
---

Add this function following CLAUDE.md: $ARGUMENTS

1. Pick the trigger: event (pub/sub topic), queue or storage notification (input binding), or drop folder. Scheduled work does not belong here: use the scheduler-dapr template.
2. Build the use case from the bottom up: Accessor(s) for any new I/O (API calls through `IHttpClientFactory`, storage or bindings through `DaprClient`), then Engine(s) for pure logic, then the Manager method. Every public member gets XML docs.
3. Write the Function class in `Functions/`: thin, one Manager call, and map permanent failures to Drop (pub/sub) or acknowledge (bindings). Make it idempotent.
4. Wire it in `FunctionsSetup` (`WithTopic`, or `MapBinding`). For a new binding, add the component YAML to `dapr/components/` with `route`, and a cloud example to `dapr/examples/`.
5. Add Engine and Manager unit tests. Extend `HostTests` if you added a topic or route.
6. Add a request to `samples/requests.http` that triggers it through the sidecar.
7. Run `dotnet build` and `dotnet test` (or `docker build .`). All architecture tests must pass, with zero warnings.
