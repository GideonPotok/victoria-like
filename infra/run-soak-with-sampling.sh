#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5001}"
CLIENTS="${SOAK_CLIENTS:-40}"
AUTH_CLIENTS="${SOAK_AUTH_CLIENTS:-20}"
DURATION_SECONDS="${SOAK_DURATION_SECONDS:-1800}"
SAMPLE_INTERVAL_SECONDS="${SOAK_SAMPLE_INTERVAL_SECONDS:-30}"
START_SERVER="${SOAK_START_SERVER:-true}"
RUN_ID="${SOAK_RUN_ID:-$(date -u +%Y%m%dT%H%M%SZ)}"
OUT_DIR="${SOAK_OUT_DIR:-soak-runs/$RUN_ID}"

SERVER_PROJECT="server/src/VictoriaLike.Server/VictoriaLike.Server.csproj"
HARNESS_PROJECT="server/tests/VictoriaLike.LoadTest/VictoriaLike.LoadTest.csproj"
POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-victoria-postgres}"
POSTGRES_DB="${POSTGRES_DB:-victoria_world}"
POSTGRES_USER="${POSTGRES_USER:-victoria}"

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
        # rss/vsz are KB on macOS and Linux.
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

write_report() {
    local exception_count
    local error_count
    local memory_summary
    local rss_first
    local rss_last
    local rss_delta

    exception_count="$(grep -Eci '"@x":|unhandled exception' "$SERVER_LOG" 2>/dev/null || true)"
    error_count="$(grep -Eci '"@l":"(Error|Fatal)"' "$SERVER_LOG" 2>/dev/null || true)"
    memory_summary="$(csv_first_last "$MEMORY_CSV" 3)"
    rss_first="$(echo "$memory_summary" | cut -d, -f1)"
    rss_last="$(echo "$memory_summary" | cut -d, -f2)"
    rss_delta="$(echo "$memory_summary" | cut -d, -f3)"

    cat > "$REPORT" <<EOF
# Soak Test Report

Run id: \`$RUN_ID\`

Command:

\`\`\`bash
SOAK_CLIENTS=$CLIENTS SOAK_AUTH_CLIENTS=$AUTH_CLIENTS SOAK_DURATION_SECONDS=$DURATION_SECONDS infra/run-soak-with-sampling.sh
\`\`\`

## Artifacts

- Server log: \`$SERVER_LOG\`
- Harness log: \`$HARNESS_LOG\`
- Memory samples: \`$MEMORY_CSV\`
- Postgres write samples: \`$DB_CSV\`

## External Samples

| Signal | Result |
| --- | ---: |
| Server RSS first | ${rss_first} KB |
| Server RSS last | ${rss_last} KB |
| Server RSS delta | ${rss_delta} KB |
| Memory sample count | $(csv_sample_count "$MEMORY_CSV") |
| DB sample count | $(csv_sample_count "$DB_CSV") |
| DB xact commit delta | $(csv_delta "$DB_CSV" 2) |
| DB xact commit/min | $(csv_delta_per_minute "$DB_CSV" 2) |
| DB xact rollback delta | $(csv_delta "$DB_CSV" 3) |
| DB tuple insert delta | $(csv_delta "$DB_CSV" 4) |
| DB tuple insert/min | $(csv_delta_per_minute "$DB_CSV" 4) |
| DB tuple update delta | $(csv_delta "$DB_CSV" 5) |
| DB tuple update/min | $(csv_delta_per_minute "$DB_CSV" 5) |
| DB tuple delete delta | $(csv_delta "$DB_CSV" 6) |
| DB tuple delete/min | $(csv_delta_per_minute "$DB_CSV" 6) |
| Server error/fatal log lines | $error_count |
| Server exception log lines | $exception_count |

## Harness Summary

\`\`\`text
$(grep -A 80 'FAKE CLIENT HARNESS V2 REPORT' "$HARNESS_LOG" 2>/dev/null || tail -n 120 "$HARNESS_LOG" 2>/dev/null || true)
\`\`\`
EOF
}

require_tool dotnet
require_tool curl
require_tool docker
require_tool lsof

echo "timestamp,pid,rss_kb,vsz_kb,pcpu,pmem" > "$MEMORY_CSV"
echo "timestamp,xact_commit,xact_rollback,tup_inserted,tup_updated,tup_deleted,blks_read,blks_hit,temp_bytes" > "$DB_CSV"

echo "Starting dependencies..."
make up

if [[ "$START_SERVER" == "true" ]]; then
    echo "Starting server; log: $SERVER_LOG"
    dotnet run --project "$SERVER_PROJECT" > "$SERVER_LOG" 2>&1 &
    SERVER_PID="$!"
else
    SERVER_PID="$(find_server_pid)"
    if [[ -z "$SERVER_PID" ]]; then
        echo "SOAK_START_SERVER=false but no server process was found for $BASE_URL" >&2
        exit 1
    fi
    echo "Using existing server pid $SERVER_PID. Server log exception count will only include logs captured outside this script."
fi

wait_for_server
sample_memory "$SERVER_PID"
sample_db

echo "Running harness; log: $HARNESS_LOG"
dotnet run --project "$HARNESS_PROJECT" -- \
    --url="$BASE_URL" \
    --clients="$CLIENTS" \
    --auth-clients="$AUTH_CLIENTS" \
    --duration="$DURATION_SECONDS" 2>&1 | tee "$HARNESS_LOG" &
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

echo "Soak artifacts written to $OUT_DIR"
echo "Report: $REPORT"
exit "$HARNESS_STATUS"
