---
description: Execute the implementation plan by processing and executing all tasks defined in tasks.md
handoffs:
  - label: Create Pull Request
    agent: devspark.create-pr
    prompt: Draft a pull request for the implemented changes
    send: true
  - label: Run Analysis
    agent: devspark.analyze
    prompt: Analyze spec consistency after implementation
scripts:
  sh: .devspark/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks
  ps: .devspark/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Workflow Position

Delivery gateway between authoring (`specify → clarify → plan → tasks → analyze + critic`) and shipping (`create-pr → pr-review`). Also the resume point for `/devspark.quickfix`.

- **Owns**: executing `tasks.md` in dependency order; keeping `tasks.md`, `spec.md` (story status), and `plan.md` (implementation notes) in sync **as work happens**; flipping `spec.md` lifecycle (`Draft → In Progress → Complete`); enforcing upstream gates before any code is written.
- **Does NOT own**: re-specifying scope (`/devspark.specify` or `/devspark.quickfix`), re-planning (`/devspark.plan`), adding/removing tasks (`/devspark.tasks`), producing gate artifacts (`/devspark.analyze`, `/devspark.critic`, `/devspark.clarify`, `/devspark.checklist`), the PR itself (`/devspark.create-pr`, `/devspark.pr-review`).
- **If scope grows mid-implementation**: halt, mark partial work in `tasks.md`, route back to the appropriate authoring command. Never silently expand scope.

## Definition of Done

Done when: every task in `tasks.md` is `[X]`, every phase has a `**Checkpoint**: Phase complete` line, every finished user story in `spec.md` carries `✅ Complete`, `spec.md` **Status** is `Complete`, and the step-4 gate pre-flight table re-run shows no regression. If any condition can't be met in one pass, stop and report exactly which one is unmet — don't keep narrating remaining steps.

**Chat output budget**: `tasks.md`/`spec.md`/`plan.md` carry full detail. In chat, report progress at phase checkpoints (one line per phase), not one line per task, plus the step 9 final summary. Don't restate file contents already written to disk.

## Constitution Authority

Load `/.documentation/memory/constitution.md` at step 4. Treat every mandated principle as **non-negotiable**:

- Missing task for a runtime-bearing principle (observability, accessibility, security baseline, test coverage, audit logging, telemetry) with no matching `## Constitution Waivers` entry in `plan.md` → **halt** and route to `/devspark.tasks`. Do not add the task yourself.
- An implementation choice that conflicts with a principle MUST be refused even if the task description appears to permit it → amend via `/devspark.plan` or propose via `/devspark.evolve-constitution`.
- Preserve `## Constitution Waivers` from `plan.md` through to the PR.

## Genuine Fix Discipline

Before resolving task-linked gates or marking findings complete, apply
`templates/command-preamble-contract.md` §9. A task that only improves a metric
without proving the behavioral intent in the finding's `intent_cue` remains
unresolved.

## Outline

**Multi-app support**: If this repository uses multi-app mode (`.documentation/devspark.json` exists with `mode: "multi-app"`), check for `--app <id>` in the user input to scope this workflow to a specific application. When app context is provided, resolve artifacts from `{app.path}/.documentation/` instead of the repository root `.documentation/`. Print the resolved scope (app name, doc root) at the start of output.

> **Script Resolution**: Before running `{SCRIPT}`, apply the 2-tier override check — if `.documentation/scripts/powershell/<filename>` (PowerShell) or `.documentation/scripts/bash/<filename>` (Bash) exists on disk, run that file instead, preserving all arguments. Team overrides in `.documentation/scripts/` always take priority over `.devspark/scripts/`.

1. Run `{SCRIPT}` from repo root and parse FEATURE_DIR and AVAILABLE_DOCS list. All paths must be absolute. For single quotes in args like "I'm Groot", use escape syntax: e.g 'I'\''m Groot' (or double-quote if possible: "I'm Groot").

2. **Check checklists status** (if FEATURE_DIR/checklists/ exists):
   - Scan all checklist files in the checklists/ directory
   - For each checklist, count:
     - Total items: All lines matching `- [ ]` or `- [X]` or `- [x]`
     - Completed items: Lines matching `- [X]` or `- [x]`
     - Incomplete items: Lines matching `- [ ]`
   - Create a status table:

     ```text
     | Checklist | Total | Completed | Incomplete | Status |
     |-----------|-------|-----------|------------|--------|
     | ux.md     | 12    | 12        | 0          | ✓ PASS |
     | test.md   | 8     | 5         | 3          | ✗ FAIL |
     | security.md | 6   | 6         | 0          | ✓ PASS |
     ```

   - Calculate overall status:
     - **PASS**: All checklists have 0 incomplete items
     - **FAIL**: One or more checklists have incomplete items

   - **If any checklist is incomplete**:
     - Display the table with incomplete item counts
       - Ask: "Some checklists are incomplete. Do you want to proceed with implementation anyway? (yes/no)"
     - Wait for user response before continuing
     - If user says "no" or "wait" or "stop", halt execution
       - If user says "yes" or "proceed" or "continue", proceed to step 3 and record the explicit override in `tasks.md` under `## Gate Acknowledgements`
     - **Autonomy override**: if `--auto` (or a standing autonomy instruction) is in effect, skip the wait — proceed to step 3 and record `auto-selected: true` in the Gate Acknowledgement instead of waiting for a reply.

   - **If all checklists are complete**:
     - Display the table showing all checklists passed
     - Automatically proceed to step 3

3. **Update Spec Status to In Progress**:
   - Read `FEATURE_DIR/spec.md`
   - If the `**Status**:` field is `Draft`, update it to `In Progress`
   - This ensures the spec is no longer marked as Draft once implementation begins
   - Preserve the lifecycle comment if present: `**Status**: In Progress <!-- Valid: Draft | In Progress | Complete -->`

4. Load and analyze the implementation context:
   - **REQUIRED**: Read tasks.md for the complete task list and execution plan
   - **REQUIRED**: Read plan.md for tech stack, architecture, file structure, and any `## Constitution Waivers`
   - **REQUIRED**: Read `/.documentation/memory/constitution.md` and extract mandated principles (see Constitution Authority above)
   - **IF EXISTS**: Read data-model.md for entities and relationships
   - **IF EXISTS**: Read contracts/ for API specifications and test requirements
   - **IF EXISTS**: Read research.md for technical decisions and constraints
   - **IF EXISTS**: Read quickstart.md for integration scenarios

   **Gate pre-flight (hard halt on failure unless the user explicitly overrides)**:

   Before writing any code, verify each gate below. Present results as a single table, then act on the worst finding.

   | Gate                            | Source of truth                             | Pass condition                                                  | On fail                                                                      |
   | ------------------------------- | ------------------------------------------- | --------------------------------------------------------------- | ---------------------------------------------------------------------------- |
   | Clarifications resolved         | `spec.md`                                   | No `[NEEDS CLARIFICATION]` markers remain                       | Route to `/devspark.clarify`                                                 |
   | Required gates from frontmatter | `spec.md` YAML `required_gates`             | Each listed gate has a matching artifact in FEATURE_DIR         | Route to the listed gate command                                             |
   | Analyze findings                | FEATURE_DIR/analysis.md (or analyze output) | No `severity: critical` findings with `status: open`            | Route to `/devspark.analyze` (or `/devspark.address-pr-review`-style triage) |
   | Critic findings                 | FEATURE_DIR/critique.md (or critic output)  | No `severity: critical` findings with `status: open`            | Route to `/devspark.critic`                                                  |
   | Checklists                      | step 2 result                               | All checklists at 0 incomplete (or explicit override recorded)  | Already handled in step 2                                                    |
   | Constitution coverage           | constitution.md vs tasks.md                 | Every runtime-bearing mandated principle has a task OR a waiver | Route to `/devspark.tasks` (regenerate) or `/devspark.plan` (record waiver)  |
   | Plan waivers acknowledged       | `plan.md` `## Constitution Waivers`         | All waivers have rationale + expiry                             | Route to `/devspark.plan`                                                    |

   If any required gate fails, surface the failure with context and ask the user whether to (a) fix first (recommended), (b) review findings, or (c) proceed anyway. If the user proceeds anyway, append or update a `## Gate Acknowledgements` section in `tasks.md` with: the failing gate(s), the unresolved findings (by ID), the user's explicit decision, and a UTC timestamp. This section will be surfaced in the PR body by `/devspark.create-pr`.

   **Autonomy override**: if `--auto` (or a standing autonomy instruction) is in effect, auto-select option (a) — route to `/devspark.tasks` for gate remediation (or the listed gate command) instead of waiting for a reply — and record `auto-selected: true` in the Gate Acknowledgement. **Exception, never auto-overridden**: a failing "Constitution coverage" or "Plan waivers acknowledged" row, or any finding whose severity is SHOWSTOPPER or carries a `§`-coded constitution citation — those always halt and wait for a human, `--auto` or not.

   The YAML frontmatter classification and `required_gates` in `spec.md` are authoritative; if the prose disagrees with the metadata, treat the metadata as truth and flag the mismatch.

5. **Project Setup Verification**:
   - **REQUIRED**: Create/verify ignore files based on actual project setup:

   **Detection & Creation Logic**:
   - Check if the following command succeeds to determine if the repository is a git repo (create/verify .gitignore if so):

     ```sh
     git rev-parse --git-dir 2>/dev/null
     ```

   - Check if Dockerfile\* exists or Docker in plan.md → create/verify .dockerignore
   - Check if .eslintrc\* exists → create/verify .eslintignore
   - Check if eslint.config.\* exists → ensure the config's `ignores` entries cover required patterns
   - Check if .prettierrc\* exists → create/verify .prettierignore
   - Check if .npmrc or package.json exists → create/verify .npmignore (if publishing)
   - Check if terraform files (\*.tf) exist → create/verify .terraformignore
   - Check if .helmignore needed (helm charts present) → create/verify .helmignore

   **If ignore file already exists**: Verify it contains essential patterns, append missing critical patterns only
   **If ignore file missing**: Create with full pattern set for detected technology

   **Common Patterns by Technology** (from plan.md tech stack):
   - **Node.js/JavaScript/TypeScript**: `node_modules/`, `dist/`, `build/`, `*.log`, `.env*`
   - **Python**: `__pycache__/`, `*.pyc`, `.venv/`, `venv/`, `dist/`, `*.egg-info/`
   - **Java**: `target/`, `*.class`, `*.jar`, `.gradle/`, `build/`
   - **C#/.NET**: `bin/`, `obj/`, `*.user`, `*.suo`, `packages/`
   - **Go**: `*.exe`, `*.test`, `vendor/`, `*.out`
   - **Ruby**: `.bundle/`, `log/`, `tmp/`, `*.gem`, `vendor/bundle/`
   - **PHP**: `vendor/`, `*.log`, `*.cache`, `*.env`
   - **Rust**: `target/`, `debug/`, `release/`, `*.rs.bk`, `*.rlib`, `*.prof*`, `.idea/`, `*.log`, `.env*`
   - **Kotlin**: `build/`, `out/`, `.gradle/`, `.idea/`, `*.class`, `*.jar`, `*.iml`, `*.log`, `.env*`
   - **C++**: `build/`, `bin/`, `obj/`, `out/`, `*.o`, `*.so`, `*.a`, `*.exe`, `*.dll`, `.idea/`, `*.log`, `.env*`
   - **C**: `build/`, `bin/`, `obj/`, `out/`, `*.o`, `*.a`, `*.so`, `*.exe`, `Makefile`, `config.log`, `.idea/`, `*.log`, `.env*`
   - **Swift**: `.build/`, `DerivedData/`, `*.swiftpm/`, `Packages/`
   - **R**: `.Rproj.user/`, `.Rhistory`, `.RData`, `.Ruserdata`, `*.Rproj`, `packrat/`, `renv/`
   - **Universal**: `.DS_Store`, `Thumbs.db`, `*.tmp`, `*.swp`, `.vscode/`, `.idea/`

   **Tool-Specific Patterns**:
   - **Docker**: `node_modules/`, `.git/`, `Dockerfile*`, `.dockerignore`, `*.log*`, `.env*`, `coverage/`
   - **ESLint**: `node_modules/`, `dist/`, `build/`, `coverage/`, `*.min.js`
   - **Prettier**: `node_modules/`, `dist/`, `build/`, `coverage/`, `package-lock.json`, `yarn.lock`, `pnpm-lock.yaml`
   - **Terraform**: `.terraform/`, `*.tfstate*`, `*.tfvars`, `.terraform.lock.hcl`
   - **Kubernetes/k8s**: `*.secret.yaml`, `secrets/`, `.kube/`, `kubeconfig*`, `*.key`, `*.crt`

6. Parse tasks.md structure and extract:
   - **Task phases**: Setup, Foundational, User Story phases (priority order), Polish
   - **Task dependencies**: Sequential vs parallel execution rules
   - **Task details**: ID, description, file paths, parallel markers [P]
   - **Execution flow**: Order and dependency requirements

7. Execute implementation phase-by-phase, in the order parsed in step 6:
   - Complete each phase fully (including its tests, per TDD) before moving to the next; verify completion before proceeding
   - Within a phase, respect sequential dependencies; run `[P]`-marked tasks together
   - Within a phase, write test tasks before the implementation tasks they cover
   - Tasks touching the same file must run sequentially even if marked `[P]`

8. Progress tracking, artifact sync, and error handling:

   **Continuous artifact sync** (required — do this as work happens, not at the end):
   - **tasks.md** — mark each task `[X]` immediately on completion. Never batch updates at the end of a phase. If a task is partially done, leave it `[ ]` and add a brief `<!-- WIP: ... -->` note rather than half-checking it.
   - **tasks.md phase checkpoints** — when every task in a phase (Setup, Foundational, User Story N, Polish) is `[X]`, append a checkpoint line under that phase heading: `**Checkpoint**: Phase complete — YYYY-MM-DD`. For user-story phases, this is the signal that the story is independently shippable.
   - **spec.md user stories** — when all tasks tagged `[USn]` are `[X]`, update the corresponding `### User Story n` heading by appending `✅ Complete` (preserve the priority marker). This keeps the spec a live picture of delivered scope.
   - **spec.md lifecycle status** — flip `**Status**: Draft` to `**Status**: In Progress` on the first completed task (already done in step 3); the final flip to `Complete` happens in step 10.
   - **plan.md** — if implementation discovers a deviation from the plan (different library chosen, contract adjusted, etc.), update plan.md inline and add a short `## Implementation Notes` entry dated and linked to the task ID. Do NOT silently diverge.
   - **Constitution waivers** — if a new waiver becomes necessary mid-implementation, **halt**, route the user to `/devspark.plan` to record it, then resume. Never invent waivers from within implement.
   - **Gate finding resolution** — when a task whose description includes `(resolves: <finding_id>[, <finding_id>...])` is marked `[X]`, verify the finding's `intent_cue` under Genuine Fix Discipline, then flip each referenced finding's `status` from `open` to `resolved` and fill in `outcome` (one line: what changed, which commit/task, and what behavior was proven) in the gate file that originated it (`gates/critic.md` for `critic-*` ids, `gates/analyze.md` for `analyze-*` ids). This is what lets a re-run of `/devspark.critic`/`/devspark.analyze` converge instead of re-reporting the same finding.

   **Progress reporting and error handling**:
   - Update tasks.md per task as it completes (already required above); in chat, report progress at phase checkpoints only — one line per phase, not one line per task
   - Halt execution if any non-parallel task fails; report it immediately regardless of checkpoint batching
   - For parallel tasks `[P]`, continue with successful tasks, report failed ones with context
   - Provide clear error messages with context for debugging
   - Suggest next steps if implementation cannot proceed

   **Governance expectations for the create-pr/pr-review handoff**:
   - Delivery status must be met (`create_pr_ready=true` in latest harness result)
   - Branch sync must pass (`HEAD` not behind `origin/main`)
   - Every `## Gate Acknowledgements` entry and every `## Constitution Waivers` entry will be surfaced by `/devspark.create-pr` in the PR body — make sure they are accurate.

9. Completion validation:
    - Verify all required tasks in `tasks.md` are `[X]`
    - Verify every phase has a `**Checkpoint**: Phase complete` line (per step 8 sync rules)
    - Verify every completed user story in `spec.md` carries the `✅ Complete` marker
    - Check that implemented features match the original specification
    - Validate that tests pass and coverage meets requirements
    - Confirm the implementation follows the technical plan (and that any deviations are recorded under `## Implementation Notes` in `plan.md`)
    - Re-run the gate pre-flight table from step 4 and confirm nothing regressed during implementation
    - Report final status with summary of completed work, any remaining `## Gate Acknowledgements`, and any active `## Constitution Waivers`
    - For quick-spec and full-spec routes, recommend `/devspark.create-pr` as the default next step after implementation

10. **Spec Lifecycle Status Update**:
    - After all tasks in tasks.md are marked `[X]` (complete):
      1. Read `FEATURE_DIR/spec.md`
      2. Update the `**Status**:` field from `Draft` or `In Progress` to `Complete`
         - Find the line matching `**Status**:` and replace its value with `Complete`
         - Preserve the lifecycle comment if present: `**Status**: Complete <!-- Valid: Draft | In Progress | Complete -->`
      3. Report: "Spec status updated to Complete — all tasks finished."
      4. Recommend `/devspark.create-pr` to draft or update the pull request before `/devspark.pr-review`
    - If any tasks remain incomplete (`- [ ]`):
      1. Update spec status to `In Progress` (if currently `Draft`)
      2. Report which tasks are still incomplete
      3. Do NOT mark spec as `Complete`

Note: This command assumes a complete task breakdown exists in tasks.md. If tasks are incomplete or missing, suggest running `/devspark.tasks` first to regenerate the task list.
