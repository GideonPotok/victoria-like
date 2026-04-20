#!/usr/bin/env bash
# Day 50 restart recovery torture harness.
#
# Requires a development server already running with PostgreSQL and Redis.
# The script intentionally leaves process stop/start as manual steps so it does
# not kill an unrelated dotnet process in a dirty local environment.

set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5001}"
SOAK_SECONDS="${DAY50_SOAK_SECONDS:-600}"
REPORT_PATH="${DAY50_REPORT_PATH:-restart_recovery_test_report.runtime.md}"

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

require_tool() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo -e "${RED}Missing required tool: $1${NC}"
        exit 1
    fi
}

api_get() {
    curl -fsS "$BASE_URL$1"
}

api_post() {
    local path="$1"
    local body="${2:-{}}"
    curl -fsS -X POST "$BASE_URL$path" \
        -H "Content-Type: application/json" \
        -d "$body"
}

wait_for_server() {
    local label="$1"
    echo -e "${BLUE}Waiting for server: $label${NC}"
    for _ in $(seq 1 60); do
        if curl -fsS "$BASE_URL/health" >/dev/null 2>&1; then
            echo -e "${GREEN}Server is responding${NC}"
            return 0
        fi
        sleep 1
    done
    echo -e "${RED}Server did not respond at $BASE_URL${NC}"
    exit 1
}

uuid() {
    if command -v uuidgen >/dev/null 2>&1; then
        uuidgen | tr '[:upper:]' '[:lower:]'
    else
        printf '00000000-0000-4000-8000-%012d\n' "$RANDOM$RANDOM"
    fi
}

login() {
    local username="$1"
    local password="$2"
    jq -n --arg username "$username" --arg password "$password" \
        '{username:$username,password:$password}' |
        curl -fsS -X POST "$BASE_URL/api/auth/login" \
            -H "Content-Type: application/json" \
            -d @-
}

authed_get() {
    local token="$1"
    local path="$2"
    curl -fsS "$BASE_URL$path" -H "Authorization: Bearer $token"
}

authed_post() {
    local token="$1"
    local path="$2"
    local body="$3"
    curl -fsS -X POST "$BASE_URL$path" \
        -H "Authorization: Bearer $token" \
        -H "Content-Type: application/json" \
        -d "$body"
}

submit_tax_command() {
    local token="$1"
    local country_id="$2"
    local tick="$3"
    local tax_rate="$4"
    local command_id="$5"
    local idem_key="$6"

    local body
    body="$(jq -n \
        --arg commandId "$command_id" \
        --arg idempotencyKey "$idem_key" \
        --arg countryId "$country_id" \
        --argjson expectedWorldTick "$tick" \
        --argjson taxRate "$tax_rate" \
        '{
            commandId:$commandId,
            idempotencyKey:$idempotencyKey,
            expectedWorldTick:$expectedWorldTick,
            commandType:"ChangeTaxRate",
            payload:{countryId:$countryId,newTaxRate:$taxRate}
        }')"

    authed_post "$token" "/api/world/commands" "$body"
}

submit_build_command() {
    local token="$1"
    local province_id="$2"
    local tick="$3"

    local body
    body="$(jq -n \
        --arg commandId "$(uuid)" \
        --arg idempotencyKey "day50-build-$(date +%s)" \
        --arg provinceId "$province_id" \
        --argjson expectedWorldTick "$tick" \
        '{
            commandId:$commandId,
            idempotencyKey:$idempotencyKey,
            expectedWorldTick:$expectedWorldTick,
            commandType:"QueueBuilding",
            payload:{provinceId:$provinceId,buildingType:"farm"}
        }')"

    authed_post "$token" "/api/world/commands" "$body"
}

capture_state() {
    local dir="$1"
    mkdir -p "$dir"
    api_get "/dev/metrics" > "$dir/metrics.json"
    api_get "/api/world/summary" > "$dir/world-summary.json"
    api_get "/api/admin/summary" > "$dir/admin-summary.json"
    api_get "/api/world/countries" > "$dir/countries.json"
    api_get "/api/world/provinces" > "$dir/provinces.json"
    api_get "/api/world/buildings/queue" > "$dir/building-queue.json"
    api_get "/api/world/market" > "$dir/market.json"
    api_get "/api/admin/commands?limit=20" > "$dir/commands.json"
}

summarize_state() {
    local dir="$1"
    jq -n \
        --slurpfile metrics "$dir/metrics.json" \
        --slurpfile world "$dir/world-summary.json" \
        --slurpfile admin "$dir/admin-summary.json" \
        --slurpfile countries "$dir/countries.json" \
        --slurpfile queue "$dir/building-queue.json" \
        --slurpfile market "$dir/market.json" \
        '{
            tick:$metrics[0].tick_count,
            world_date:$metrics[0].world_timestamp,
            world_summary:$world[0],
            pending_commands:$admin[0].pending_commands,
            invariant_violations:($admin[0].invariant_violations | length),
            latest_snapshot:$admin[0].latest_snapshot,
            countries:($countries[0] | map({tag,name,tax_rate,treasury,province_count})),
            building_queue_count:($queue[0] | length),
            market_tick:$market[0].tick,
            market_goods:($market[0].goods | map({id,price,supply,demand}))
        }'
}

append_report() {
    local before_dir="$1"
    local after_dir="$2"
    local savepoint_json="$3"
    local tax_command_json="$4"
    local duplicate_json="$5"
    local build_command_json="$6"
    local pre_restart_relogin_json="$7"
    local post_restart_relogin_json="$8"

    local before_tick
    local after_tick
    local before_queue
    local after_queue
    local invariant_count
    before_tick="$(jq -r '.tick_count' "$before_dir/metrics.json")"
    after_tick="$(jq -r '.tick_count' "$after_dir/metrics.json")"
    before_queue="$(jq -r 'length' "$before_dir/building-queue.json")"
    after_queue="$(jq -r 'length' "$after_dir/building-queue.json")"
    invariant_count="$(jq -r '.invariant_violations | length' "$after_dir/admin-summary.json")"

    {
        echo "# Day 50 Restart Recovery Runtime Report"
        echo
        echo "Generated: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
        echo
        echo "## Run Configuration"
        echo
        echo "- Base URL: \`$BASE_URL\`"
        echo "- Soak duration: \`$SOAK_SECONDS\` seconds"
        echo "- Manual restart: yes"
        echo
        echo "## Results"
        echo
        echo "- Tick before restart: \`$before_tick\`"
        echo "- Tick after restart: \`$after_tick\`"
        echo "- Building queue before restart: \`$before_queue\`"
        echo "- Building queue after restart: \`$after_queue\`"
        echo "- Post-restart invariant violations: \`$invariant_count\`"
        echo
        echo "## Command Responses"
        echo
        echo "Tax command:"
        echo '```json'
        echo "$tax_command_json" | jq '.'
        echo '```'
        echo
        echo "Duplicate retry:"
        echo '```json'
        echo "$duplicate_json" | jq '.'
        echo '```'
        echo
        echo "Building command:"
        echo '```json'
        echo "$build_command_json" | jq '.'
        echo '```'
        echo
        echo "Pre-restart relogin/me response:"
        echo '```json'
        echo "$pre_restart_relogin_json" | jq '.'
        echo '```'
        echo
        echo "Post-restart relogin/me response:"
        echo '```json'
        echo "$post_restart_relogin_json" | jq '.'
        echo '```'
        echo
        echo "Savepoint:"
        echo '```json'
        echo "$savepoint_json" | jq '.'
        echo '```'
        echo
        echo "## Before Summary"
        echo '```json'
        summarize_state "$before_dir"
        echo '```'
        echo
        echo "## After Summary"
        echo '```json'
        summarize_state "$after_dir"
        echo '```'
    } > "$REPORT_PATH"
}

require_tool curl
require_tool jq

WORK_DIR="$(mktemp -d)"
BEFORE_DIR="$WORK_DIR/before"
AFTER_DIR="$WORK_DIR/after"

echo -e "${BLUE}Day 50 restart recovery torture test${NC}"
echo "Base URL: $BASE_URL"
echo "Soak duration: $SOAK_SECONDS seconds"
echo

wait_for_server "initial"

echo -e "${BLUE}Seeding development passwords if needed${NC}"
api_post "/dev/seed-passwords" "{}" >/dev/null || true

echo -e "${BLUE}Logging in as england-player${NC}"
LOGIN_JSON="$(login "england-player" "eng123")"
TOKEN="$(echo "$LOGIN_JSON" | jq -r '.token')"
COUNTRY_ID="$(echo "$LOGIN_JSON" | jq -r '.controlled_country_id')"
if [[ -z "$TOKEN" || "$TOKEN" == "null" || -z "$COUNTRY_ID" || "$COUNTRY_ID" == "null" ]]; then
    echo -e "${RED}Login failed or did not return controlled country${NC}"
    exit 1
fi

PROVINCE_ID="$(api_get "/api/world/provinces" | jq -r --arg country "$COUNTRY_ID" '.[] | select(.owner_id == $country) | .id' | head -n 1)"
if [[ -z "$PROVINCE_ID" || "$PROVINCE_ID" == "null" ]]; then
    echo -e "${RED}No owned province found for controlled country $COUNTRY_ID${NC}"
    exit 1
fi

echo -e "${BLUE}Capturing baseline state${NC}"
capture_state "$BEFORE_DIR"
BASE_TICK="$(jq -r '.tick_count' "$BEFORE_DIR/metrics.json")"

echo -e "${BLUE}Submitting commands during active ticks${NC}"
TAX_COMMAND_ID="$(uuid)"
TAX_IDEM_KEY="day50-tax-$TAX_COMMAND_ID"
TAX_RESPONSE="$(submit_tax_command "$TOKEN" "$COUNTRY_ID" "$BASE_TICK" 17 "$TAX_COMMAND_ID" "$TAX_IDEM_KEY")"
DUPLICATE_RESPONSE="$(submit_tax_command "$TOKEN" "$COUNTRY_ID" "$BASE_TICK" 17 "$TAX_COMMAND_ID" "$TAX_IDEM_KEY")"
BUILD_RESPONSE="$(submit_build_command "$TOKEN" "$PROVINCE_ID" "$BASE_TICK")"

echo "Tax response: $(echo "$TAX_RESPONSE" | jq -r '.status')"
echo "Duplicate response: $(echo "$DUPLICATE_RESPONSE" | jq -r '.status')"
echo "Build response: $(echo "$BUILD_RESPONSE" | jq -r '.status')"

echo -e "${BLUE}Soaking with periodic tax commands${NC}"
SOAK_DEADLINE=$((SECONDS + SOAK_SECONDS))
TAX_RATE=18
while (( SECONDS < SOAK_DEADLINE )); do
    CURRENT_TICK="$(api_get "/dev/metrics" | jq -r '.tick_count')"
    COMMAND_ID="$(uuid)"
    submit_tax_command "$TOKEN" "$COUNTRY_ID" "$CURRENT_TICK" "$TAX_RATE" "$COMMAND_ID" "day50-soak-$COMMAND_ID" >/dev/null || true
    TAX_RATE=$((TAX_RATE + 1))
    if (( TAX_RATE > 25 )); then
        TAX_RATE=18
    fi
    sleep 10
done

echo -e "${BLUE}Testing disconnect/reconnect via logout and relogin${NC}"
curl -fsS -X POST "$BASE_URL/api/auth/logout" -H "Authorization: Bearer $TOKEN" >/dev/null
RELOGIN_JSON="$(login "england-player" "eng123")"
TOKEN="$(echo "$RELOGIN_JSON" | jq -r '.token')"
ME_JSON="$(authed_get "$TOKEN" "/api/auth/me")"
echo "Reconnected actor: $(echo "$ME_JSON" | jq -r '.actor_id')"

echo -e "${BLUE}Creating named savepoint${NC}"
SAVEPOINT_JSON="$(api_post "/api/admin/snapshots" '{"name":"day50-torture"}')"
echo "Savepoint: $(echo "$SAVEPOINT_JSON" | jq -r '.file_name')"

echo
echo -e "${YELLOW}Stop the server process now, then start it again.${NC}"
echo "Recommended command: cd server && dotnet run --project src/VictoriaLike.Server"
echo -e "${BLUE}Press Enter after the restarted server is listening...${NC}"
read -r

wait_for_server "after manual restart"

echo -e "${BLUE}Testing post-restart login and country mapping${NC}"
POST_RESTART_LOGIN_JSON="$(login "england-player" "eng123")"
POST_RESTART_TOKEN="$(echo "$POST_RESTART_LOGIN_JSON" | jq -r '.token')"
POST_RESTART_COUNTRY_ID="$(echo "$POST_RESTART_LOGIN_JSON" | jq -r '.controlled_country_id')"
POST_RESTART_ME_JSON="$(authed_get "$POST_RESTART_TOKEN" "/api/auth/me")"

echo -e "${BLUE}Capturing post-restart state${NC}"
capture_state "$AFTER_DIR"

BEFORE_TICK="$(jq -r '.tick_count' "$BEFORE_DIR/metrics.json")"
AFTER_TICK="$(jq -r '.tick_count' "$AFTER_DIR/metrics.json")"
INVARIANTS_AFTER="$(jq -r '.invariant_violations | length' "$AFTER_DIR/admin-summary.json")"
SAVEPOINT_PRESENT="$(jq -r '.recent_snapshots | any(.savepoint_name == "day50-torture")' "$AFTER_DIR/admin-summary.json")"

append_report "$BEFORE_DIR" "$AFTER_DIR" "$SAVEPOINT_JSON" "$TAX_RESPONSE" "$DUPLICATE_RESPONSE" "$BUILD_RESPONSE" "$ME_JSON" "$POST_RESTART_ME_JSON"

echo
if (( AFTER_TICK >= BEFORE_TICK )); then
    echo -e "${GREEN}PASS: tick did not move backward ($BEFORE_TICK -> $AFTER_TICK)${NC}"
else
    echo -e "${RED}FAIL: tick moved backward ($BEFORE_TICK -> $AFTER_TICK)${NC}"
    exit 1
fi

if (( INVARIANTS_AFTER == 0 )); then
    echo -e "${GREEN}PASS: no post-restart invariant violations${NC}"
else
    echo -e "${RED}FAIL: post-restart invariant violations detected ($INVARIANTS_AFTER)${NC}"
    exit 1
fi

if [[ "$POST_RESTART_COUNTRY_ID" == "$COUNTRY_ID" ]]; then
    echo -e "${GREEN}PASS: controlled country mapping survived restart${NC}"
else
    echo -e "${RED}FAIL: controlled country mapping changed ($COUNTRY_ID -> $POST_RESTART_COUNTRY_ID)${NC}"
    exit 1
fi

if [[ "$SAVEPOINT_PRESENT" == "true" ]]; then
    echo -e "${GREEN}PASS: named day50-torture savepoint is visible after restart${NC}"
else
    echo -e "${RED}FAIL: named day50-torture savepoint is not visible after restart${NC}"
    exit 1
fi

echo -e "${GREEN}Day 50 runtime report written to $REPORT_PATH${NC}"
