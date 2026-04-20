#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5001}"
SERVER_PORT="${BASE_URL##*:}"
SERVER_PORT="${SERVER_PORT%%/*}"
WARMUP_SECONDS="${NBOMBER_SOAK_WARMUP_SECONDS:-10}"
SAMPLE_INTERVAL_SECONDS="${NBOMBER_SOAK_SAMPLE_INTERVAL_SECONDS:-30}"
START_SERVER="${NBOMBER_SOAK_START_SERVER:-true}"
PROFILE="${NBOMBER_SOAK_PROFILE:-soak}"
if [[ "$PROFILE" == "two-player-soak" ]]; then
    TOTAL_USERS="${NBOMBER_SOAK_TOTAL_USERS:-2}"
    AUTH_USERS="${NBOMBER_SOAK_AUTH_USERS:-2}"
    DURATION_SECONDS="${NBOMBER_SOAK_DURATION_SECONDS:-1800}"
else
    TOTAL_USERS="${NBOMBER_SOAK_TOTAL_USERS:-40}"
    AUTH_USERS="${NBOMBER_SOAK_AUTH_USERS:-20}"
    DURATION_SECONDS="${NBOMBER_SOAK_DURATION_SECONDS:-1800}"
fi
COMMAND_MIX="${NBOMBER_SOAK_COMMAND_MIX:-}"
RUN_ID="${NBOMBER_SOAK_RUN_ID:-$(date -u +%Y%m%dT%H%M%SZ)}"
OUT_DIR="${NBOMBER_SOAK_OUT_DIR:-nbomber-soak-runs/$RUN_ID}"

SERVER_PROJECT="server/src/VictoriaLike.Server/VictoriaLike.Server.csproj"
HARNESS_PROJECT="server/tests/VictoriaLike.NBomberLoadTest/VictoriaLike.NBomberLoadTest.csproj"
POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-victoria-postgres}"
POSTGRES_DB="${POSTGRES_DB:-victoria_world}"
POSTGRES_USER="${POSTGRES_USER:-victoria}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-victoria_dev_password}"
POSTGRES_PORT="${POSTGRES_PORT:-5432}"
REDIS_CONTAINER="${REDIS_CONTAINER:-victoria-redis}"
REDIS_PORT="${REDIS_PORT:-6379}"

SERVER_LOG="$OUT_DIR/server.log"
HARNESS_LOG="$OUT_DIR/harness.log"
MEMORY_CSV="$OUT_DIR/server-memory.csv"
DB_CSV="$OUT_DIR/postgres-writes.csv"
REPORT="$OUT_DIR/soak_test_report.md"

SERVER_PID=""
HARNESS_PID=""
SAMPLER_PID=""

mkdir -p "$OUT_DIR"

cleanup() {
    if [[ -n "$SAMPLER_PID" ]] && kill -0 "$SAMPLER_PID" >/dev/null 2>&1; then
        kill "$SAMPLER_PID" >/dev/null 2>&1 || true
    fi

    if [[ -n "$HARNESS_PID" ]] && kill -0 "$HARNESS_PID" >/dev/null 2>&1; then
        kill "$HARNESS_PID" >/dev/null 2>&1 || true
    fi

    if [[ "$START_SERVER" == "true" && -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" >/dev/null 2>&1; then
        kill "$SERVER_PID" >/dev/null 2>&1 || true
    fi
}

trap cleanup EXIT INT TERM

require_tool() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "Missing required tool: $1" >&2
        exit 1
    fi
}

container_exists() {
    docker container inspect "$1" >/dev/null 2>&1
}

wait_for_container() {
    local container="$1"
    local status

    for _ in $(seq 1 60); do
        status="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$container" 2>/dev/null || true)"
        if [[ "$status" == "healthy" || "$status" == "running" ]]; then
            return 0
        fi
        sleep 1
    done

    echo "Container $container did not become ready" >&2
    exit 1
}

ensure_dependency_container() {
    local service="$1"
    local container="$2"

    if container_exists "$container"; then
        echo "Using existing $container container"
        docker start "$container" >/dev/null
    else
        if [[ "$service" == "postgres" ]]; then
            echo "Creating $container container on host port $POSTGRES_PORT"
            docker run -d \
                --name "$container" \
                -e POSTGRES_USER="$POSTGRES_USER" \
                -e POSTGRES_PASSWORD="$POSTGRES_PASSWORD" \
                -e POSTGRES_DB="$POSTGRES_DB" \
                -p "$POSTGRES_PORT:5432" \
                --health-cmd "pg_isready -U $POSTGRES_USER" \
                --health-interval 10s \
                --health-timeout 5s \
                --health-retries 5 \
                postgres:15-alpine >/dev/null
        elif [[ "$service" == "redis" ]]; then
            echo "Creating $container container on host port $REDIS_PORT"
            docker run -d \
                --name "$container" \
                -p "$REDIS_PORT:6379" \
                --health-cmd "redis-cli ping" \
                --health-interval 10s \
                --health-timeout 5s \
                --health-retries 5 \
                redis:7-alpine >/dev/null
        else
            echo "Unsupported dependency service: $service" >&2
            exit 1
        fi
    fi

    wait_for_container "$container"
}

start_dependencies() {
    echo "Starting PostgreSQL and Redis..."
    ensure_dependency_container postgres "$POSTGRES_CONTAINER"
    ensure_dependency_container redis "$REDIS_CONTAINER"
    docker ps --filter "name=$POSTGRES_CONTAINER" --filter "name=$REDIS_CONTAINER" || true
}

wait_for_server() {
    for _ in $(seq 1 90); do
        if curl -fsS "$BASE_URL/health/ready" >/dev/null 2>&1; then
            return 0
        fi
        sleep 1
    done

    echo "Server did not become ready at $BASE_URL" >&2
    exit 1
}

seed_dev_passwords() {
    local response

    response="$(curl -fsS -X POST "$BASE_URL/dev/seed-passwords" 2>/dev/null || true)"
    if [[ -n "$response" ]]; then
        echo "Seeded dev passwords: $response"
    else
        echo "Dev password seed endpoint unavailable or returned no response; continuing"
    fi
}

find_server_pid() {
    local port
    port="${BASE_URL##*:}"
    port="${port%%/*}"
    lsof -nP -tiTCP:"$port" -sTCP:LISTEN | head -n 1 || true
}

sample_memory() {
    local pid="$1"
    local timestamp
    timestamp="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

    if [[ -n "$pid" ]] && kill -0 "$pid" >/dev/null 2>&1; then
        ps -p "$pid" -o rss=,vsz=,%cpu=,%mem= 2>/dev/null |
            awk -v ts="$timestamp" -v pid="$pid" '{gsub(/^ +| +$/, ""); print ts "," pid "," $1 "," $2 "," $3 "," $4}' >> "$MEMORY_CSV" || true
    else
        echo "$timestamp,$pid,,,,," >> "$MEMORY_CSV"
    fi
}

sample_db() {
    docker exec "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At -F, -c "
        SELECT
            now(),
            xact_commit,
            xact_rollback,
            tup_inserted,
            tup_updated,
            tup_deleted,
            blks_read,
            blks_hit,
            temp_bytes
        FROM pg_stat_database
        WHERE datname = '$POSTGRES_DB';
    " >> "$DB_CSV" 2>/dev/null || true
}

sample_loop() {
    local server_pid="$1"
    local harness_pid="$2"

    while kill -0 "$harness_pid" >/dev/null 2>&1; do
        sample_memory "$server_pid"
        sample_db
        sleep "$SAMPLE_INTERVAL_SECONDS"
    done

    sample_memory "$server_pid"
    sample_db
}

csv_delta() {
    local file="$1"
    local column="$2"
    awk -F, -v col="$column" '
        NR == 2 { first = $col }
        NR > 1 { last = $col }
        END {
            if (first == "" || last == "") print "n/a";
            else print last - first;
        }
    ' "$file"
}

csv_first_last() {
    local file="$1"
    local column="$2"
    awk -F, -v col="$column" '
        NR == 2 { first = $col }
        NR > 1 { last = $col }
        END {
            if (first == "" || last == "") print "n/a,n/a,n/a";
            else print first "," last "," last - first;
        }
    ' "$file"
}

csv_sample_count() {
    local file="$1"
    awk 'NR > 1 { count++ } END { print count + 0 }' "$file"
}

csv_delta_per_minute() {
    local file="$1"
    local column="$2"
    awk -F, -v col="$column" '
        NR == 2 { first = $col }
        NR > 1 { last = $col }
        END {
            if (first == "" || last == "") print "n/a";
            else if ('"$DURATION_SECONDS"' <= 0) print "n/a";
            else printf "%.1f", (last - first) / ('"$DURATION_SECONDS"' / 60.0);
        }
    ' "$file"
}

harness_latest_counter() {
    local counter="$1"
    local report_dir
    local from_report
    report_dir="$(grep -Eo 'Reports saved in folder: "[^"]+"' "$HARNESS_LOG" 2>/dev/null | tail -n 1 | sed 's/.*: "\(.*\)"/\1/')"

    if [[ -n "$report_dir" && -d "$report_dir" ]]; then
        from_report="$(
            grep -Eho "\"MetricName\":\"$counter\"[^}]*\"Value\":[0-9.]+" "$report_dir"/*.html 2>/dev/null |
                tail -n 1 |
                sed 's/.*"Value"://'
        )"
        if [[ -n "$from_report" ]]; then
            echo "$from_report"
            return
        fi
    fi

    grep -Eo "$counter = [0-9.]+" "$HARNESS_LOG" 2>/dev/null | tail -n 1 | awk '{ print $3 }'
}

write_report() {
    local exception_count
    local error_count
    local memory_summary
    local rss_first
    local rss_last
    local rss_delta
    local latest_reports
    local nbomber_report_dir

    exception_count="$(grep -Eci '"@x":|unhandled exception' "$HARNESS_LOG" 2>/dev/null || true)"
    error_count="$(grep -Eci '"@l":"(Error|Fatal)"' "$HARNESS_LOG" 2>/dev/null || true)"
    memory_summary="$(csv_first_last "$MEMORY_CSV" 3)"
    rss_first="$(echo "$memory_summary" | cut -d, -f1)"
    rss_last="$(echo "$memory_summary" | cut -d, -f2)"
    rss_delta="$(echo "$memory_summary" | cut -d, -f3)"
    nbomber_report_dir="$(grep -Eo 'Reports saved in folder: "[^"]+"' "$HARNESS_LOG" 2>/dev/null | tail -n 1 | sed 's/.*: "\(.*\)"/\1/')"
    if [[ -n "$nbomber_report_dir" && -d "$nbomber_report_dir" ]]; then
        latest_reports="$(find "$OUT_DIR" "$nbomber_report_dir" -maxdepth 2 -type f \( -name '*.html' -o -name '*.json' -o -name '*.md' -o -name '*.csv' -o -name '*.txt' \) | sort | tail -n 40)"
    else
        latest_reports="$(find "$OUT_DIR" -maxdepth 2 -type f \( -name '*.html' -o -name '*.json' -o -name '*.md' \) | sort | tail -n 20)"
    fi

    {
        echo "# NBomber Soak Test Report"
        echo
        echo "Run id: \`$RUN_ID\`"
        echo
        echo "Command:"
        echo
        echo '```bash'
        if [[ -n "$COMMAND_MIX" ]]; then
            echo "NBOMBER_SOAK_PROFILE=$PROFILE NBOMBER_SOAK_COMMAND_MIX=$COMMAND_MIX NBOMBER_SOAK_TOTAL_USERS=$TOTAL_USERS NBOMBER_SOAK_AUTH_USERS=$AUTH_USERS NBOMBER_SOAK_DURATION_SECONDS=$DURATION_SECONDS infra/run-nbomber-soak-with-sampling.sh"
        else
            echo "NBOMBER_SOAK_PROFILE=$PROFILE NBOMBER_SOAK_TOTAL_USERS=$TOTAL_USERS NBOMBER_SOAK_AUTH_USERS=$AUTH_USERS NBOMBER_SOAK_DURATION_SECONDS=$DURATION_SECONDS infra/run-nbomber-soak-with-sampling.sh"
        fi
        echo '```'
        echo
        echo "## Artifacts"
        echo
        echo "- Server log: \`$SERVER_LOG\`"
        echo "- Harness log: \`$HARNESS_LOG\`"
        echo "- Memory samples: \`$MEMORY_CSV\`"
        echo "- Postgres write samples: \`$DB_CSV\`"
        echo
        echo "## External Samples"
        echo
        echo "| Signal | Result |"
        echo "| --- | ---: |"
        echo "| Server RSS first | ${rss_first} KB |"
        echo "| Server RSS last | ${rss_last} KB |"
        echo "| Server RSS delta | ${rss_delta} KB |"
        echo "| Memory sample count | $(csv_sample_count "$MEMORY_CSV") |"
        echo "| DB sample count | $(csv_sample_count "$DB_CSV") |"
        echo "| DB xact commit delta | $(csv_delta "$DB_CSV" 2) |"
        echo "| DB xact commit/min | $(csv_delta_per_minute "$DB_CSV" 2) |"
        echo "| DB xact rollback delta | $(csv_delta "$DB_CSV" 3) |"
        echo "| DB tuple insert delta | $(csv_delta "$DB_CSV" 4) |"
        echo "| DB tuple insert/min | $(csv_delta_per_minute "$DB_CSV" 4) |"
        echo "| DB tuple update delta | $(csv_delta "$DB_CSV" 5) |"
        echo "| DB tuple update/min | $(csv_delta_per_minute "$DB_CSV" 5) |"
        echo "| DB tuple delete delta | $(csv_delta "$DB_CSV" 6) |"
        echo "| DB tuple delete/min | $(csv_delta_per_minute "$DB_CSV" 6) |"
        echo "| Server error/fatal log lines | $error_count |"
        echo "| Server exception log lines | $exception_count |"
        echo
        echo "## Final Harness Counters"
        echo
        echo "| Counter | Result |"
        echo "| --- | ---: |"
        echo "| command_http_accepted | $(harness_latest_counter command_http_accepted) |"
        echo "| command_http_rejected | $(harness_latest_counter command_http_rejected) |"
        echo "| command_http_errored | $(harness_latest_counter command_http_errored) |"
        echo "| command_results_applied | $(harness_latest_counter command_results_applied) |"
        echo "| command_results_rejected | $(harness_latest_counter command_results_rejected) |"
        echo "| command_results_failed | $(harness_latest_counter command_results_failed) |"
        echo "| commands_sent | $(harness_latest_counter commands_sent) |"
        echo "| peaceful_commands_sent | $(harness_latest_counter peaceful_commands_sent) |"
        echo "| duplicate_retries | $(harness_latest_counter duplicate_retries) |"
        echo "| reconnect_successes | $(harness_latest_counter reconnect_successes) |"
        echo "| reconnect_snapshots | $(harness_latest_counter reconnect_snapshots) |"
        echo "| stale_token_rejected | $(harness_latest_counter stale_token_rejected) |"
        echo "| unexpected_ws_errors | $(harness_latest_counter unexpected_ws_errors) |"
        echo "| ws_connect_failures | $(harness_latest_counter ws_connect_failures) |"
        echo "| world_updates | $(harness_latest_counter world_updates) |"
        echo "| mean_tick_interval_ms | $(harness_latest_counter mean_tick_interval_ms) |"
        echo
        echo "## NBomber Output"
        echo
        echo '```text'
        grep -A 120 -E 'victoria_like_nbomber_load_test|Threshold failed|Scenario|Report|Passed|Failed' "$HARNESS_LOG" 2>/dev/null || tail -n 160 "$HARNESS_LOG" 2>/dev/null || true
        echo '```'
        if [[ -n "$latest_reports" ]]; then
            echo
            echo "## Report Files"
            echo
            echo '```text'
            echo "$latest_reports"
            echo '```'
        fi
    } > "$REPORT"
}

require_tool dotnet
require_tool curl
require_tool docker
require_tool lsof

echo "timestamp,pid,rss_kb,vsz_kb,pcpu,pmem" > "$MEMORY_CSV"
echo "timestamp,xact_commit,xact_rollback,tup_inserted,tup_updated,tup_deleted,blks_read,blks_hit,temp_bytes" > "$DB_CSV"

echo "Starting dependencies..."
start_dependencies

if [[ "$START_SERVER" == "true" ]]; then
    echo "Starting server; log: $SERVER_LOG"
    ConnectionStrings__DefaultConnection="Server=localhost;Port=${POSTGRES_PORT};Database=${POSTGRES_DB};User Id=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Maximum Pool Size=50;Connection Idle Lifetime=60;" \
    ConnectionStrings__Redis="localhost:${REDIS_PORT}" \
    Kestrel__Endpoints__Http__Url="$BASE_URL" \
    Server__Port="$SERVER_PORT" \
    ASPNETCORE_ENVIRONMENT="Development" \
    ASPNETCORE_URLS="$BASE_URL" \
    dotnet run --project "$SERVER_PROJECT" > "$SERVER_LOG" 2>&1 &
    SERVER_PID="$!"
else
    SERVER_PID="$(find_server_pid)"
    if [[ -z "$SERVER_PID" ]]; then
        echo "NBOMBER_SOAK_START_SERVER=false but no server process was found for $BASE_URL" >&2
        exit 1
    fi
    echo "Using existing server pid $SERVER_PID."
fi

wait_for_server
seed_dev_passwords
sample_memory "$SERVER_PID"
sample_db

echo "Running NBomber harness; log: $HARNESS_LOG"
HARNESS_ARGS=(
    --profile="$PROFILE"
    --url="$BASE_URL"
    --total-users="$TOTAL_USERS"
    --auth-users="$AUTH_USERS"
    --duration="$DURATION_SECONDS"
    --warmup="$WARMUP_SECONDS"
)
if [[ -n "$COMMAND_MIX" ]]; then
    HARNESS_ARGS+=(--command-mix="$COMMAND_MIX")
fi

dotnet run --project "$HARNESS_PROJECT" -- \
    "${HARNESS_ARGS[@]}" 2>&1 | tee "$HARNESS_LOG" &
HARNESS_PID="$!"

sample_loop "$SERVER_PID" "$HARNESS_PID" &
SAMPLER_PID="$!"

set +e
wait "$HARNESS_PID"
HARNESS_STATUS="$?"
set -e

if [[ -n "$SAMPLER_PID" ]] && kill -0 "$SAMPLER_PID" >/dev/null 2>&1; then
    kill "$SAMPLER_PID" >/dev/null 2>&1 || true
fi

write_report

echo "NBomber soak artifacts written to $OUT_DIR"
echo "Report: $REPORT"
exit "$HARNESS_STATUS"
