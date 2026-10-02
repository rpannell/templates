---
description: Add a feature across the iDesign layers (model, Dto, Entity, Accessor, Engine, Manager, Controller, tests)
argument-hint: <what the feature does>
---

Add this feature following CLAUDE.md: $ARGUMENTS

1. Work out which use case (Manager), business rules (Engine) and I/O (Accessor) it needs. Extend existing components when the responsibility already exists, and create new ones only for a distinct responsibility.
2. Build it in the order from "Adding a Feature" in CLAUDE.md. Give every public type and member XML docs, and give controller actions the full set (`summary`, `param`, `returns`, one `response` per `[ProducesResponseType]`).
3. If it needs new tables or columns, list the Liquibase changeset it needs. Don't add EF migrations.
4. Add unit tests for new Engine logic and Manager orchestration.
5. Run `dotnet build` and `dotnet test`, or `docker build .` if there's no local SDK. All architecture and documentation tests must pass.
6. Summarize what changed in each layer.
