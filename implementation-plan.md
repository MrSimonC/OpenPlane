# OpenPlane Implementation Plan

## Objective
Build a desktop-first (.NET MAUI on macOS + Windows) Copilot-SDK-powered coworking agent app with safe scoped local access, autonomous execution, MCP connectors, model configurability, and local encrypted history.

## Current Status (Implemented)
- .NET solution + project structure created (`OpenPlane.sln`, `src/*`, `tests/*`).
- Core abstractions and domain models established for planning, runs, policies, connectors, models, and history.
- Storage layer scaffolded with local encrypted history repository + JSON model selection store.
- In-memory MCP connector broker and connector registry scaffolded.
- MAUI app shell implemented with:
  - model selection + persistence
  - auth status panel
  - login + refresh auth actions
  - single-button run workflow
  - timeline display
- Copilot SDK integrated (`GitHub.Copilot.SDK`) with real prompt execution path.
- CLI path resolution logic added for Copilot CLI invocation and NVM/node-host fallback logic.
- Unit and integration tests added for core policy/orchestration/model behavior.
- Execution cancellation support added in app UI/runtime (`Stop` button + in-flight cancellation propagation).

## Known Gaps / Issues to Resolve
1. MacCatalyst host policy and environment variability can still affect external process startup in some machines.
2. Plan decomposition quality is currently heuristic-first and can be further improved with richer model guidance and evaluation loops.

## Implementation Principles
- Keep `gpt-5-mini` as default model, with runtime discovery + user override.
- Keep local-only history with encryption at rest.
- Enforce explicit folder grants for file operations.
- Require plan approval before non-trivial autonomous execution.
- Prefer deterministic, typed interfaces with constructor injection.

## Phase Plan

### Phase 1: Stabilize Runtime and Auth (High Priority)
Goal: make Copilot execution reliable on macOS and Windows.

Tasks:
- [x] Add `Execution Mode` setting:
  - `Embedded CLI process` (current)
  - `External Copilot endpoint` (connect to already-running service via `CliUrl`)
- [x] Add robust auth diagnostics panel:
  - effective CLI command/path
  - auth status details
  - last startup error
- [x] Implement fallback auth flow:
  - in-app login attempt
  - capture device-flow login output and parse `user code` + verification URL
  - show device code in dedicated UI field with one-click `Copy Code` action
  - show `Open Verification Page` action to launch the browser URL directly
  - keep device code visible until auth state changes or user dismisses
  - clear terminal command guidance when blocked
- [x] Add app startup health checks:
  - CLI executable accessibility
  - version detection
  - model list probe

Deliverables:
- Reliable auth and run experience for common local setups.
- Clear user-facing error states for unsupported execution conditions.

Acceptance:
- User can confirm auth state in app.
- User can complete device login entirely from app UI by copying the displayed device code.
- User can run a prompt and receive assistant output in timeline on supported environments.

### Phase 2: Workspace, Grants, and Policy Enforcement
Goal: implement real scoped local access controls.

Tasks:
- [x] Add workspace model and settings UI:
  - workspace list
  - granted folders list/add/remove
- [x] Wire `IAccessPolicyService` to all file operations.
- [x] Implement file tools in execution layer:
  - read file
  - search files
  - write/patch file
  - create file/folder
- [x] Ensure canonical path checks and traversal prevention.
- [x] Add policy violation event types and timeline reporting.

Deliverables:
- End-to-end scoped file access in runtime behavior.

Acceptance:
- Files outside grants cannot be read/modified.
- Attempts outside scope are denied and surfaced in timeline.

### Phase 3: Plan/Approve + Multi-step Autonomous Execution
Goal: restore full cowork workflow over real operations.

Tasks:
- [x] Add plan generation using model-assisted step decomposition.
- [x] Reintroduce plan approval gate (configurable strictness).
- [x] Persist run sessions and steps (status transitions).
- [x] Implement resumable multi-step execution loop.
- [x] Stream structured run events to timeline.

Deliverables:
- Plan->approve->execute lifecycle with persistence.

Acceptance:
- No autonomous run starts without required approval.
- User sees step-level progress and terminal state.

### Phase 4: Worker Isolation and Execution Boundary
Goal: move execution out of UI process into isolated worker.

Tasks:
- [x] Implement worker host protocol (IPC):
  - request/response envelopes
  - event streaming
- [x] Run file/network tool execution in worker process.
- [x] Add worker lifecycle and heartbeat supervision.
- [x] Route all privileged operations through worker boundary.

Deliverables:
- Isolated execution architecture with clear boundary.

Acceptance:
- UI process no longer performs direct file mutation/network actions for agent tools.

### Phase 5: Network Allowlist Enforcement
Goal: enforce outbound network policy.

Tasks:
- [x] Add per-workspace allowlist editor UI.
- [x] Include default preset (`GitHub + Copilot endpoints`).
- [x] Enforce allowlist in all app/worker HTTP clients.
- [x] Add explicit deny event reporting.

Deliverables:
- Effective outbound control with user-managed policy.

Acceptance:
- Non-allowlisted domains are blocked and logged.

### Phase 6: MCP Connectors (Generic MCP)
Goal: deliver practical connector support.

Tasks:
- [x] Replace in-memory connector broker with real connector lifecycle manager.
- [x] Add connector config persistence (command, env, scopes).
- [x] Start/stop and health-check connectors.
- [x] Expose connector status and failures in UI.
- [x] Integrate connector tools into agent execution context.

Deliverables:
- Config-driven connector management and use in runs.

Acceptance:
- User-configured MCP connectors can connect and be used during execution.

### Phase 7: File-Type Adapter Layer
Goal: broader file support.

Tasks:
- [x] Text-native editing (md/txt/json/yaml/xml/toml/csv/code) harden and test.
- [x] Add extract-only adapters for complex formats:
  - PDF
  - Office docs
  - spreadsheets
  - presentations
  - images
  - notebooks
- [x] Provide clear fallback behavior when write-back unsupported.

Deliverables:
- Predictable file handling matrix by type + capability.

Acceptance:
- Supported formats are correctly parsed/handled with explicit limitations surfaced.

### Phase 8: History, Telemetry, and Recovery
Goal: production-grade local persistence and observability.

Tasks:
- [x] Add schema versioning/migrations for local storage.
- [x] Harden encrypted history key handling and rotation path.
- [x] Add crash recovery for in-progress runs.
- [x] Add structured local logs (opt-in) and export for diagnostics.

Deliverables:
- Durable local state and debuggability.

Acceptance:
- App restart preserves history, model selection, and run state integrity.

## Test Strategy
- Unit tests:
  - policy checks
  - orchestration state transitions
  - model/default selection behavior
  - network allowlist matching
- Integration tests:
  - approved plan executes scoped file operations
  - policy denials handled correctly
  - connector lifecycle paths
- UI validation:
  - auth state transitions
  - run timeline updates
  - model selection persistence
  - workspace grant management
- Manual validation matrix:
  - macOS + Windows desktop
  - authenticated/unauthenticated scenarios
  - varying CLI install paths

## Backlog (After Core Delivery)
- Browser pairing mode (explicitly out of current v1 scope).
- Advanced approval strategies (per-step/per-tool).
- Enterprise auth modes and managed policy packs.
- Rich diff review UI and patch application UX.

## Definition of Done (v1)
- Desktop app supports authenticated Copilot prompt execution with visible timeline.
- `gpt-5-mini` default model with user-configurable selection from discovered models.
- Scoped folder grants enforced for agent file operations.
- Plan approval workflow gates autonomous multi-step execution.
- MCP connectors usable through config-driven setup.
- Local history encrypted at rest and stored only on device.
- Build/test pipelines pass with documented platform prerequisites.
