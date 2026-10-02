#!/bin/sh
# Changelog rules that Liquibase itself doesn't enforce. Runs in the Docker build; any error fails the build.
#   - Every <changeSet> has an explicit <rollback> (a changeSet that only tags the database is exempt).
#   - The <changeSet ...> opening tag, with its id, is on one line.
#   - Changeset ids are unique across the whole changelog.
#   - Every XML file except the master changelog is included by another changelog.
set -eu

dir="${1:-changelog}"
master="changelog.xml"
status=0

for file in $(find "$dir" -name '*.xml' | sort); do
    awk -v file="$file" '
        { sub(/\r$/, "") }
        /<changeSet[ \t>]/ {
            if ($0 !~ /id="[^"]+"/) { printf "ERROR %s:%d: put <changeSet id=\"...\" author=\"...\"> on one line\n", file, NR; bad = 1 }
            match($0, /id="[^"]+"/); id = substr($0, RSTART + 4, RLENGTH - 5)
            open = 1; rollback = 0; tag = 0; other = 0; line = NR
        }
        open && /<rollback[ \t>\/]/ { rollback = 1 }
        open && /<tagDatabase[ \t\/]/ { tag = 1 }
        open && /<(createTable|addColumn|createIndex|addForeignKeyConstraint|addPrimaryKey|addUniqueConstraint|dropTable|dropColumn|dropIndex|renameTable|renameColumn|modifyDataType|insert|update|delete|loadData|loadUpdateData|sql|sqlFile|createView|dropView|createSequence|addNotNullConstraint|dropNotNullConstraint|addDefaultValue|dropDefaultValue|customChange|createProcedure)[ \t>\/]/ { other = 1 }
        /<\/changeSet>/ {
            if (open && !rollback && !(tag && !other)) {
                printf "ERROR %s:%d: changeSet \"%s\" has no <rollback>\n", file, line, id; bad = 1
            }
            open = 0
        }
        END { exit bad }
    ' "$file" || status=1

    name=$(basename "$file")
    if [ "$file" != "$dir/$master" ] && ! grep -rqs --include='*.xml' "file=\"[^\"]*$name\"" "$dir"; then
        echo "ERROR $file: not included by any changelog"
        status=1
    fi
done

duplicates=$(grep -rhos --include='*.xml' '<changeSet id="[^"]*"' "$dir" | sort | uniq -d)
if [ -n "$duplicates" ]; then
    echo "ERROR duplicate changeSet ids:"
    echo "$duplicates"
    status=1
fi

if [ "$status" -eq 0 ]; then
    echo "Changelog checks passed."
fi
exit "$status"
