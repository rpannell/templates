# syntax=docker/dockerfile:1
# check=error=true
#
# Migration image: the changelog baked into Liquibase. Running it applies every pending changeset (up).
# Pass another command to go down or inspect, for example:
#   docker run --rm <image> rollback --tag=1.0.0
#   docker run --rm <image> status --verbose

# Liquibase 5.x no longer ships database drivers, so add the PostgreSQL JDBC driver.
FROM liquibase/liquibase:5.0.3 AS base
RUN lpm add postgresql --global

# Build-time checks. A broken changelog fails the image build, the same way a compile error would.
FROM base AS check
USER root
WORKDIR /check
COPY changelog/ changelog/
COPY scripts/check-changelog.sh scripts/
# 1. Rules: every changeSet has an explicit <rollback> (or only tags), ids are unique, every file is included.
# 2. Liquibase validates every file against the XSD, parses the changelog and renders the SQL offline (no database).
RUN sh scripts/check-changelog.sh changelog \
 && liquibase --show-banner=false --analytics-enabled=false \
      --url=offline:postgresql \
      --search-path=/check/changelog \
      --changelog-file=changelog.xml \
      update-sql --context-filter=dev > /dev/null \
 && echo "Offline update-sql rendered successfully."

FROM base AS final
# Copying from the check stage makes the checks a required step of every build.
COPY --from=check /check/changelog /liquibase/changelog
COPY scripts/verify-rollback.sh /liquibase/scripts/verify-rollback.sh

# Changeset identity includes the file path, so the search path and changelog name must never change.
ENV LIQUIBASE_SEARCH_PATH=/liquibase/changelog \
    LIQUIBASE_COMMAND_CHANGELOG_FILE=changelog.xml \
    LIQUIBASE_COMMAND_CONTEXT_FILTER="!dev" \
    LIQUIBASE_SHOW_BANNER=false \
    LIQUIBASE_ANALYTICS_ENABLED=false

# Connection comes from the environment at run time (never baked in):
#   LIQUIBASE_COMMAND_URL=jdbc:postgresql://<host>:5432/<database>
#   LIQUIBASE_COMMAND_USERNAME / LIQUIBASE_COMMAND_PASSWORD
CMD ["update"]
