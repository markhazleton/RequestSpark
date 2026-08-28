# DevSpark Command Preamble Contract

Shared command rules in this file are normative for command prompts that cite
them. Commands may add stricter behavior, but must not weaken these sections.

## 9. Genuine Fix Discipline

Commands that fix, review, audit, analyze, or verify work MUST resolve the
behavioral intent of a finding before treating a metric as satisfied. A smaller
lint count, lower complexity score, higher coverage percentage, or cleaner audit
score is supporting evidence only. It is not proof of a genuine fix unless the
observable behavior, user outcome, safety property, or contract obligation that
motivated the finding is repaired or preserved.

When a finding is metric-related, report the behavior first and the metric
second. When a remediation changes only measurement shape while leaving behavior
unchanged, mark the finding unresolved.

### 9.1 Intent Cues

Use `intent_cue` or `Intent` fields to state the behavior the finding protects
or repairs before recommending metric movement.

| Finding trigger | Intent cue must identify | Acceptable evidence |
| ---------------- | ------------------------ | ------------------- |
| Lint, formatting, or static-analysis warning | The runtime, readability, portability, or maintainability behavior the warning protects | Code path explanation plus targeted test, reproduction, or reviewer-checkable invariant |
| Complexity, duplication, or size metric | The decision boundary or behavior that became easier to validate without changing semantics accidentally | Before/after behavior equivalence plus a focused test or fixture |
| Coverage metric | The user-visible path, edge case, failure mode, or contract now exercised | Test name, assertion, fixture, and behavior under test |
| Security, reliability, or performance score | The trust boundary, failure mode, or performance outcome at risk | Exploit/failure scenario, benchmark, log signal, policy check, or regression test |

Cross-language note: JavaScript, TypeScript, C#, and Java fixes often make a
metric pass through type narrowing, null guards, exception handling, async
rewrites, or dependency updates. The intent cue must still name the behavior
being protected, such as authorization continuity, idempotency, cancellation,
resource cleanup, input validation, or API compatibility.

### 9.2 Constitution Citation Hook

When a finding protects a constitution principle, include the principle citation
next to the intent cue. The citation must explain why the behavior matters under
that principle. Do not cite a constitution section as a label-only decoration.

Examples:

- `intent_cue: Preserve JSON contract compatibility for current consumers (Constitution §I Backward Compatibility).`
- `Intent: Prove the fix repairs authorization behavior, not only a security score (Constitution §IV Governance Authority).`
