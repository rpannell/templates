#!/usr/bin/env bash
# Builds and tests every template, the same way CI does. Run it before pushing.
#   ./build.sh               everything (needs the .NET 10 SDK and Docker)
#   ./build.sh --skip-docker only the .NET steps
# Output is also written to artifacts/build.log; a PASS/FAIL summary is printed at the end.
set -uo pipefail

root="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$root/artifacts"
log="$root/artifacts/build.log"
echo "Build started $(date -u +%FT%TZ)" > "$log"
skip_docker=false
[[ "${1:-}" == "--skip-docker" ]] && skip_docker=true

summary=()
failed=false

step() {   # step "<name>" "<dir>" "<command>" ["<command>"...]
    local name="$1" dir="$2"; shift 2
    local start=$SECONDS ok=true
    printf '\n=== %s ===\n' "$name" | tee -a "$log"
    for cmd in "$@"; do
        echo "> $cmd" >> "$log"
        if ! (cd "$dir" && bash -c "$cmd") 2>&1 | tee -a "$log"; then ok=false; break; fi
    done
    local result=PASS; $ok || { result=FAIL; failed=true; }
    summary+=("$(printf '%-45s %s %5ss' "$name" "$result" $((SECONDS - start)))")
}

step "dotnet build + test: api-idesign"    "$root/templates/api-idesign" \
     "dotnet build Company.Product.slnx -c Release" "dotnet test Company.Product.slnx -c Release --no-build"
step "dotnet build + test: worker-dapr"    "$root/templates/worker-dapr" \
     "dotnet build Company.Product.Worker.slnx -c Release" "dotnet test Company.Product.Worker.slnx -c Release --no-build"
step "dotnet build + test: worker-rebus"   "$root/templates/worker-rebus" \
     "dotnet build Company.Product.Worker.slnx -c Release" "dotnet test Company.Product.Worker.slnx -c Release --no-build"
step "dotnet build + test: scheduler-dapr" "$root/templates/scheduler-dapr" \
     "dotnet build Company.Product.Scheduler.slnx -c Release" "dotnet test Company.Product.Scheduler.slnx -c Release --no-build"
step "dotnet pack: template package" "$root" \
     "dotnet pack CodingTemplates.csproj -c Release -o artifacts/packages"

if ! $skip_docker; then
    for name in api-idesign worker-dapr worker-rebus scheduler-dapr db-liquibase; do
        step "docker build: $name" "$root/templates/$name" "docker build -t coding-templates/$name:local ."
    done
    step "liquibase: rollback round trip" "$root/templates/db-liquibase" \
         "docker compose --profile verify run --rm --build verify" "docker compose --profile verify down -v"
    step "Dockerfile.verify: generated templates" "$root" "docker build -f Dockerfile.verify ."
fi

printf '\n=== Summary ===\n' | tee -a "$log"
printf '%s\n' "${summary[@]}" | tee -a "$log"
echo "Full log: $log"
$failed && exit 1 || exit 0
