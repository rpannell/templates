# Company.Product.Database: Liquibase Migrations

This project owns the database schema. Every schema change is a Liquibase changeset in **XML**, with an explicit rollback. The Docker image built from this folder is the only thing that changes the schema. Applications (the API) never run migrations or DDL, and EF Core migrations are not used.

## Layout

```
changelog/
├── changelog.xml                  master: includes releases in order (never reorder or remove)
└── releases/
    ├── baseline.xml               tag "baseline" = empty database
    └── 1.0.0/
        ├── changelog.xml          includes this release's files, then tags "1.0.0"
        ├── 0001-create-orders.xml
        ├── 0002-create-order-lines.xml
        └── 0003-seed-dev-orders.xml   context="dev": local sample data only
scripts/
├── check-changelog.sh             build-time rules (below)
└── verify-rollback.sh             round-trip test: up, down to baseline, up
Dockerfile                         liquibase/liquibase:5.0.3 + PostgreSQL driver + changelog + checks
docker-compose.yml                 local PostgreSQL, migrate, verify (profile)
```

## Rules

1. **XML changelogs only**, using Liquibase change types (`createTable`, `addColumn`, `createIndex`, `addForeignKeyConstraint`, `insert`...). Use raw `<sql>` only when no change type exists, and then give it an explicit rollback.
2. **Every `<changeSet>` has an explicit `<rollback>`**, even when Liquibase could generate one. Up and down are both reviewed in the pull request. The only exemption is a changeSet that only has `<tagDatabase>`.
3. **One changeSet = one logical change.** Liquibase runs each changeset in a transaction, so a failure only undoes that changeset.
4. **Never edit or delete a changeset that has been applied anywhere.** Its checksum is stored in `databasechangelog`, and editing it breaks every environment. Fix mistakes with a new changeset.
5. **ids are unique** across the whole changelog, formatted `NNNN-description` with the number matching the file (`0004-add-orders-status`). `author` is the team name (`product`). Put `<changeSet id="..." author="...">` on one line.
6. **File naming:** `releases/<version>/NNNN-description.xml`. Numbering continues across releases (1.1.0 starts at 0004), and each file holds one or more related changesets.
7. **Every release ends with a tag changeset** (`id="tag-<version>"`, `<tagDatabase tag="<version>"/>`) as the last item of the release changelog. That tag is the rollback target for the next release.
8. **Destructive changes are split across releases.** For example, rename a column by adding the new column (release N), backfilling and switching the API (N), then dropping the old column (N+1). A rollback of `dropTable` or `dropColumn` can't restore data, so its `<rollback>` recreates the structure only. Say so in a `<comment>`.
9. **Sample or test data** uses `context="dev"`. The image defaults to `LIQUIBASE_COMMAND_CONTEXT_FILTER=!dev`, so it never reaches other environments.
10. **Changeset identity includes the file path**, relative to the search path `/liquibase/changelog`. Never move or rename an applied file, and never change `LIQUIBASE_SEARCH_PATH` or the master changelog name.
11. **Types:** use portable Liquibase types (`uuid`, `varchar(n)`, `int`, `bigint`, `decimal(p, s)`, `boolean`, `timestamp with time zone`, `text`). Name constraints and indexes explicitly: `pk_<table>`, `fk_<table>_<referenced>`, `ix_<table>_<columns>`, `uq_<table>_<columns>`.
12. **Keep the schema in step with the API.** Table and column names match the API's `AppDbContext` mapping (snake_case).

## Build-time checks (the "compile" step)

`docker build .` fails when:

- `scripts/check-changelog.sh` finds a changeSet without `<rollback>`, a duplicate id, a `<changeSet>` tag split across lines, or an XML file not included by any changelog.
- Liquibase can't validate a file against the XSD, parse the changelog, or render the SQL (`update-sql` against `offline:postgresql`, with no database needed).

## Verify rollbacks

`docker compose --profile verify run --rm verify` applies everything to a throwaway database, rolls back to `baseline`, and applies everything again. If any rollback is incomplete, the second update fails. Run it for every change (it's the VS Code test task).

## Adding a change

1. Create `changelog/releases/<next version>/NNNN-description.xml` (copy the header from an existing file), or add a file to the current unreleased version.
2. Write the changeSet(s), each with a `<comment>` and an explicit `<rollback>`.
3. Include the file in that release's `changelog.xml`, **before** the `tag-<version>` changeset. For a new release, create `releases/<version>/changelog.xml` ending with its tag, and include it at the end of the master `changelog.xml`.
4. Build (`docker compose build migrate`), then run the round trip (`docker compose --profile verify run --rm verify`).
5. Preview the SQL (`docker compose run --rm migrate update-sql`) and paste it into the pull request.
