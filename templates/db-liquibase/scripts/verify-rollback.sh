#!/bin/sh
# Round-trip test against a throwaway database: every changeset must apply (up), roll back (down) and apply again.
# If a rollback leaves anything behind, the second update fails (for example "relation already exists").
# Run it with: docker compose --profile verify run --rm verify
set -eu

step() { printf '\n==> %s\n' "$1"; }

step "Up: apply every changeset"
liquibase update

step "Down: roll back everything to the baseline tag"
liquibase rollback --tag=baseline

step "Up again: proves the rollbacks left a clean schema"
liquibase update

step "Status: nothing may be pending"
liquibase status --verbose

printf '\nRound trip passed: every changeset applies, rolls back and re-applies.\n'
