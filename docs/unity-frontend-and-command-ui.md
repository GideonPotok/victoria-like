# Unity Frontend and Command UI Guidelines

This document defines how Unity UI should interact with the server command layer.
It applies to work under `client-unity` and to server API behavior that directly
supports the playable frontend.

For the research plan behind future refinements to these rules, see
`docs/unity-command-pipeline-research-plan.md`.

## Core Principle

Unity is presentation and input. The server remains authoritative.

That does not mean Unity must be stateless. The client may keep local UI state,
selection state, form state, and draft player intent. The client must not compute
authoritative gameplay outcomes or assume a command succeeded until the server
accepts or applies it.

## UI Toolkit Direction

Use UI Toolkit for new dashboard and management UI:

- Define layout in UXML.
- Define visual styling in USS.
- Use C# for API calls, event binding, local draft state, and rendering server
  DTOs into UI elements.
- Avoid generating complex dashboard layouts entirely from C# unless the UI is a
  repeated runtime list or there is a specific technical reason.
- Keep MonoBehaviours thin. Prefer small client-side services for API access,
  command scheduling, and view-model shaping when a UI class starts to grow.

The Unity frontend should feel like a game dashboard: stable layout, immediate
feedback, readable state, and explicit pending/error states.

## Client State Rules

Allowed client state:

- currently selected country, province, tab, row, or control
- cached server DTOs used for rendering
- local draft values for editable controls
- pending command status and retry timers
- optimistic display of a draft value, clearly marked when not yet synced

Disallowed client state:

- authoritative world state
- duplicated gameplay calculations that decide real outcomes
- hidden economic, military, or pop simulation rules
- client-only validation that is stricter than the server and silently blocks
  valid commands

Client validation may improve UX, but the server must remain the source of
truth.

## Event vs State Decision Rule

Unity's multiplayer guidance reduces the transport choice to one practical
question: should a late-joining client receive this information? For
VictoriaLike's HTTP plus WebSocket frontend, use the same rule in app-layer
terms.

Use this decision table:

| Need | VictoriaLike shape | Late join / refresh behavior | Example |
|------|--------------------|------------------------------|---------|
| Persistent authoritative world data | Read DTO plus WebSocket-pushed state | Must be reconstructable from current server state | country budget, province owner, construction queue |
| Short-lived visual or notification event | transient WebSocket event or local UI event | May be missed and not replayed | toast, pulse animation, "command applied" flash |
| Editable unsaved player intent | local draft state only | Recreated from local interaction, not server truth | slider drag position before submit |
| Authoritative player action | command submission | Must survive retries and be reflected in later state | change tax, queue building |
| Focused details too expensive or noisy to push continuously | inspection fetch | Refetched on demand or after reconnect | detailed province inspection, deep market panel |

If the user must still see it after reconnect, refresh, or late join, model it
as authoritative state or as a fetchable server DTO, not only as a one-shot
event.

## Command Interaction Model

VictoriaLike command UX should follow an OpenRCT2-style split between preview
and mutation for commands where the player needs clear affordance before
committing:

- `Query`/preview answers "can this be done right now?" and may return
  UI-facing details such as cost, blocker reason, target position, or other
  display metadata.
- `Execute` performs the authoritative mutation.
- `Execute` should be allowed to run the same validation path first, so the
  authoritative mutation path never skips rule checks.
- Not every command needs a separate preview endpoint. Use it when the UI needs
  button enablement, reason text, cost preview, or placement feedback before
  the player commits.

Do not send one network command for every raw UI event when the control edits a
continuous or repeated value.

Frontend commands enter an authoritative ordered pipeline. Unity may shape and
schedule player intent for UX reasons, but Unity does not decide truth, final
order, or application tick.

The frontend should distinguish between:

- draft state: what the player is currently editing
- command intent: what the client has decided to submit
- authoritative state: what the server has accepted and later applied

For adjustable controls such as taxes, spending, tariffs, priorities, and
sliders:

1. Update local draft state immediately.
2. Render the draft value immediately.
3. Coalesce repeated input into the latest desired value.
4. Send deliberately after a debounce, on pointer release, on focus loss, or via
   a low-frequency command scheduler.
5. Respect server `retryAfterTicks` or other backoff signals.
6. Clear pending state only when the server accepts or applies the command.

For discrete actions such as `QueueBuilding`:

1. Send the command promptly.
2. Disable the specific action or show it as pending while awaiting a result.
3. Show accepted, rejected, or failed status in the relevant UI area.
4. Do not retry automatically unless the command is idempotent and the retry
   behavior is explicit.

## Recommended Scheduling

For value-editing controls, use a client-side scheduler:

- debounce rapid edits for roughly 300-500 ms
- send only the latest value for each logical field
- limit command sends to a small number per second
- retry cooldown rejections after `retryAfterTicks`
- keep later user edits while a previous send is pending
- prefer stable idempotency keys for exact retries where supported

Treat local scheduling as UX smoothing, not authority:

- local debounce and coalescing reduce input spam
- the server still decides the accepted order and execution timing
- a later local draft may replace an earlier unsent draft, but must not pretend
  the world already changed
- reconnect or refresh must recover from authoritative state, not from the last
  local draft alone

Use full input history only when each input matters independently to the
outcome. Boss Room sends movement inputs this way because the full sequence is
meaningful. For VictoriaLike dashboard controls, that is usually the wrong
model.

Prefer latest-desired-value semantics for:

- budget sliders
- tax rates
- spending priorities
- similar adjustable controls where only the final chosen value matters

Prefer preserving input history only for:

- future cases where each step is itself a meaningful action
- audit or replay systems that intentionally track each committed command
- interactions where intermediate inputs affect gameplay, not just UI feel

Example behavior:

- user clicks `Poor Tax +` three times
- UI immediately shows `29%`
- scheduler sends one `ChangeStrataTax` command with `rate = 0.29`
- if the server replies with cooldown, scheduler waits and retries the latest
  pending poor-tax value

The server command budget, cooldowns, idempotency, and conflict rules are safety
systems. They should not be the normal visible UX for ordinary clicking.

## Client Command Scheduler

For new Unity dashboard work, prefer a dedicated `ClientCommandScheduler`
service instead of embedding retry and coalescing rules directly inside UI
components.

Recommended responsibilities:

- own draft state for editable command fields while the user is interacting
- track one logical pending slot per field, such as `country:albion:tax:poor`
- debounce and coalesce repeated edits into the latest desired value
- submit commands through the API client with stable command IDs or
  idempotency keys when supported
- keep a later desired value queued while an earlier send is in flight
- interpret structured rejection results, especially cooldown or retry metadata
- retry only the latest still-relevant desired value after backoff
- clear stale pending entries on reconnect, logout, or authoritative overwrite
- expose a small UI-facing view model: draft value, pending state, retry time,
  last outcome, and last authoritative value

Recommended non-responsibilities:

- deciding gameplay truth
- performing hidden client-only rule enforcement
- mutating authoritative cached DTOs as if the command already applied
- retrying discrete non-idempotent actions without explicit backend support

## Scheduler Lifecycle

The scheduler lifecycle for adjustable values should be:

1. `Idle`
   The UI shows the latest authoritative value and there is no unsent draft.
2. `Drafting`
   The player changes a control. The scheduler stores the latest desired value
   and updates local display immediately.
3. `Debouncing`
   The scheduler waits briefly for more input. New edits replace the pending
   desired value rather than creating new command intents.
4. `Submitting`
   The scheduler sends the latest desired value as one authoritative command.
5. `AwaitingResult`
   The command is in flight. The UI stays responsive, but the field remains
   marked pending.
6. `RetryScheduled`
   If the server returns retry metadata such as `retryAfterTicks`, the
   scheduler waits until the backoff expires, then rechecks whether the desired
   value is still current before resubmitting.
7. `Superseded`
   If the player changes the value again while a prior send is pending, the old
   desired value becomes obsolete. The scheduler keeps only the newest desired
   value for the next send opportunity.
8. `Settled`
   The scheduler clears pending state after the authoritative result is visible
   through command outcome plus refreshed or pushed state.

The key rule is that the scheduler settles on authoritative state visibility,
not merely on local send completion.

## Logical Field Model

Treat adjustable controls as logical fields with independent scheduling slots.
Examples:

- `country:{countryId}:tax:{strata}`
- `country:{countryId}:spending:{category}`
- future tariff, subsidy, or priority controls keyed the same way

This gives one in-flight command per logical field while still allowing
different fields to be edited independently.

If two controls write to the same authoritative value, they must share one
logical field key and one scheduler slot.

## Replacement and Cancellation Rules

For latest-desired-value controls:

- unsent drafts are replaceable
- scheduled retries are replaceable
- in-flight sends are not cancelled on the server, but their follow-up retry
  decision is based on the newest desired value
- when authoritative state arrives matching the newest desired value, clear the
  slot even if an older command result arrives later

For discrete actions:

- do not model them as replaceable desired values
- track them as separate pending actions
- require explicit idempotency semantics before any automatic retry

## Reconnect and Refresh Behavior

On reconnect, refresh, or session restore, the scheduler should:

- drop retry timers that depended on a stale local clock
- reload authoritative DTOs first
- keep only drafts the user is still actively editing in the current session
- discard pending slots that cannot be matched to a still-relevant command
  history entry
- avoid replaying buffered desired values blindly after reconnect

Recovery should be conservative. It is better to show authoritative state and
let the player re-edit than to resubmit stale local intent automatically.

## Backend Contract for Frontend Commands

Server endpoints that accept frontend commands should:

- return structured command responses for accepted and rejected commands
- include stable rejection reasons suitable for client branching
- include retry/backoff metadata when the client can recover automatically
- treat duplicate exact retries idempotently when a command ID or idempotency key
  is provided
- keep deterministic command ordering and authoritative validation in the server
- expose enough read DTOs for the client to refresh after command results

Game-level command outcomes should be structured, not reduced to a single
string. The minimum frontend-safe shape is:

- outcome status: accepted, applied, rejected, or failed
- stable rejection code for branching and analytics
- concise player-facing message
- optional preview metadata such as cost, affordability, target identifiers, or
  focus position
- optional retry metadata when the client can recover automatically

The current `CommandResponse` already covers status, message, rejection reason,
and retry metadata. Day 1's backend direction is to extend the contract with an
optional preview/query result shape rather than replacing the current response.

The client should parse structured rejection responses as ordinary command
outcomes, not as unexpected transport failures. HTTP status codes can still
signal rate limits or validation failures, but the body should carry the game
level outcome.

## Query vs Execute

Use preview/query support for commands where the UI benefits from preflight
feedback:

- build or queue actions that may fail due to ownership, funds, or active
  conflicts
- map or province actions that benefit from focus location or target metadata
- actions with meaningful cost or upkeep implications

Use execute-only submission when a preview would add little value:

- simple toggles whose failure reasons are already obvious in-context
- commands where the UI can rely on current authoritative DTOs and show the
  result after submission

The preview result should never grant authority to the client. It is advisory.
The execute path still validates against current world state at submission and
execution time.

## Nested Command Rule

OpenRCT2's nested-action rule maps cleanly to VictoriaLike backend design:

- one player command should create one authoritative network command
- helper logic inside a command handler may call shared validation or mutation
  helpers locally
- helper logic inside a command handler should not enqueue a second network
  command implicitly

If a future VictoriaLike command composes smaller operations, those inner
operations should stay in-process within the authoritative handler unless they
truly need their own audit trail, ordering slot, retry semantics, or UI-visible
 identity.

This prevents accidental duplicate command sends, duplicate audit records, and
unclear ownership of command outcomes.

## Error Handling

Use different UI and log treatment for different failure classes:

- Command rejected by game rules: show a concise in-UI status, log as info or
  warning.
- Cooldown or rate limit with retry metadata: schedule retry or mark pending,
  log as warning at most.
- Network failure or malformed server response: show disconnected/error state,
  log as error.
- Authentication failure: prompt for login/reconnect flow, log as error.

Do not spam Unity Console with expected command rejections during normal play.

## Immediate UI vs Authoritative Execution

OpenRA distinguishes immediate orders from delayed gameplay orders. VictoriaLike
 should adopt the same separation in a lighter form:

- immediate local UI feedback happens instantly on the client: button pressed
  state, draft slider value, spinner, pending badge, disabled action state
- authoritative gameplay execution happens only after the server accepts and
  later applies the command in pipeline order

Do not blur these together. Immediate client feedback is required for a
responsive dashboard, but it must remain clearly provisional until the
authoritative result arrives.

## WebSocket and Refresh Flow

WebSocket updates should be the normal source of live state. HTTP fetches are
for initial load, focused inspection, recovery after reconnect, and explicit
refreshes.

Applied to VictoriaLike's transport split:

- use WebSocket for live authoritative state updates and short-lived command
  result notifications
- use HTTP for initial page data, command submission, focused inspections, and
  recovery fetches
- do not force WebSocket events to carry every inspectable detail needed by all
  screens at all times

After a command result:

- use WebSocket state updates when they are sufficient
- fetch focused detail only when the UI needs data not present in the pushed
  update
- avoid full-dashboard polling every tick

After reconnect or suspected state drift:

- discard stale pending UI that no longer maps to an in-flight command
- refresh authoritative DTOs before resuming editable controls
- prefer replay-safe command history and world snapshots over trying to rebuild
  truth from client-local interaction history

## Late Join and Refresh Rule

Late join in VictoriaLike is closer to refresh or reconnect than to spawning a
new action-game avatar. The same design consequence applies:

- durable gameplay truth must be reconstructable from the latest server state
- transient command feedback may be omitted after reconnect
- pending UI should be rebuilt from command history and current DTOs, not from
  stale local widget state alone

If a screen becomes incorrect when the user refreshes the dashboard after ten
seconds, that screen is relying too much on transient events and not enough on
authoritative state.

## Testing Expectations

When changing Unity command behavior or the command-facing backend:

- add server tests for command validation, rejection reasons, and cooldown
  metadata
- run `dotnet test server/VictoriaLike.Server.sln`
- test Unity interaction manually for repeated clicks, cooldown responses,
  reconnects, and visible pending states
- run the load harness when changing WebSocket, auth, reconnect, or command
  processing behavior

## Relationship to Command Conflict Rules

See `docs/command_conflict_rules.md` for deterministic server ordering,
idempotency, repeated command handling, stale client state, and construction
conflicts.

This document is the client-facing complement: it explains how UI should shape
player intent before sending commands into that authoritative pipeline.
