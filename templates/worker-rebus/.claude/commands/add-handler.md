---
description: Add a message handler (event or command) following the worker's iDesign and Rebus rules
argument-hint: <what should happen, and which message triggers it>
---

Add this handler following CLAUDE.md: $ARGUMENTS

1. Decide event or command. An **event** says something happened and has many possible subscribers (named `<Thing><PastTense>Event`, the worker subscribes to it). A **command** asks this worker to do something and is sent to its queue (named `<Verb><Thing>Command`). Scheduled work does not belong here: it needs a separate single-replica scheduler app.
2. Build the use case from the bottom up: Accessor(s) for any new I/O (API calls through `IHttpClientFactory`, publishing through `IBus`), then Engine(s) for pure logic, then the Manager method. Every public member gets XML docs.
3. Add the message contract to `Handlers/Models/` (no suffix), and a mapper to the Manager's Dto in `Handlers/Mapping/` if the shapes differ.
4. Write the handler in `Handlers/<Name>Handler.cs`: thin, one Manager call. Make it idempotent (delivery is at-least-once). A missing resource that retrying can't fix is logged and dropped; invalid input throws `BusinessValidationException` so it fails fast to the error queue; anything transient propagates so Rebus retries.
5. Wire it in `Handlers/Setup/HandlersSetup.cs`: `services.AddRebusHandler<...>()`, and for an event add `await bus.Subscribe<...>()` to `SubscribeToEventsAsync`.
6. Add Engine and Manager unit tests, and a `MessageHandlingTests` case for the routing and failure behaviour you rely on.
7. Add a request to `samples/requests.http` that sends the message through RabbitMQ.
8. Run `dotnet build` and `dotnet test` (or `docker build .`). All architecture tests must pass, with zero warnings.
