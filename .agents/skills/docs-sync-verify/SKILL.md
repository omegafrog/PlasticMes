---
name: docs-sync-verify
description: "Verify that documentation fully reflects execute output and actual code changes after execute-writer runs, before closer updates status and creates a PR."
---

## Purpose

1. Verify that documentation now reflects the real implementation.
2. Compare raw `execute` output, raw `execute-writer` output, and the actual changed files or diff.
3. Detect missing documentation coverage, false documentation claims, unresolved code-to-doc mismatches, and missing operational details needed for closure and PR writing.
4. Reuse the repository docs validator so docs must be both synchronized and structurally valid before closure.

## When to use

- Immediately after `execute-writer` updates docs.
- Before `closer` performs active-to-completed transition.
- Before PR creation or PR draft generation.
- Whenever the user asks whether implementation and docs are actually synchronized.

## Required Inputs

Inspect all of the following in this order:

1. `AGENTS.md`
2. `plan.md`
3. Related planning and design docs linked by `plan.md`
4. Raw `execute` output
5. Raw `execute-writer` output
6. Actual changed files, diff, and current file tree
7. Repository docs validator result from:

```bash
python3 .agents/skills/docs-verify/scripts/run.py
```

## Verification Rules

### 1. Implementation Coverage

Each completed Implementation Plan item must have corresponding code changes.
If a plan item is marked complete but no meaningful code change exists, report mismatch.
If code changed outside plan scope, report mismatch.
If implementation was intentionally partial, docs must not describe it as complete.

### 2. Execute Report Coverage

Compare docs against these execute report sections:

- Implemented Plan file path
- Modified Files
- Created Files
- Deleted or Renamed Files
- External Contract Changes
- Business Policy list
- Architectural Impact
- Documentation Impact
- Validation Results
- Remaining Risks / Follow-ups

### 3. Must-Document Changes

If any of the following exist in code, diff, or execute output, docs must reflect them or explicitly justify why doc changes are unnecessary:

- new file or directory
- deleted file
- renamed file
- moved responsibility
- public interface change
- DTO change
- event change
- config change
- environment variable change
- test strategy change
- execution command change
- dependency direction change
- architectural boundary impact
- externally visible behavior change

### 4. No False Documentation

- Docs must not say work is implemented when it is not.
- Docs must not omit unresolved mismatches already reported by execute-writer.
- Docs must not overstate architecture changes beyond actual implementation.
- Docs must not silently drop documented risks or follow-up items that still remain true after implementation.

### 5. Code-to-Doc Traceability

- Each meaningful code change must map to at least one updated doc or an explicit no-doc-update rationale.
- If execute reported documentation impact but docs did not change, mark FAIL.
- If docs changed substantially without any matching code or execute-report basis, mark FAIL unless the change is clearly validator-driven structure repair.

### 6. Verification and Runbook Coverage

- If execute changed how the feature is run, configured, tested, or validated, docs must reflect that.
- If new commands, configuration keys, or environment variables were introduced, the relevant docs must show them.
- If validation failed during execution and produced remaining caveats, those caveats must still be visible where appropriate.

### 7. PR Readiness Coverage

The combined outputs and docs must leave closer able to write a truthful PR.

Confirm that the available material is sufficient to state:

- task summary
- implementation scope
- documentation scope
- validation results
- remaining risks or follow-ups

If any of these are missing or unverifiable, mark PR_NOT_READY.

### 8. Structural Validation

Run:

```python
python3 .agents/skills/docs-verify/scripts/run.py
```

Structural success alone is not enough.
If docs are structurally valid but semantically incomplete, mark FAIL.

## Decision Policy

- Trust actual diff over prose summaries when they conflict.
- Trust actual docs over intended docs.
- Do not infer missing implementation.
- Do not infer missing documentation.
- Mark PASS only when both synchronization and structure checks pass.
- Mark PR_READY only when the closure and PR-writing inputs are sufficiently grounded.

## Output Format

```md
# Verdict

PASS | FAIL

# Missing Documentation Coverage
...
# Incorrect Documentation Claims
...
# Execute-to-Docs Trace
covered:
missing:
unclear:
# Structural Validation

PASS | FAIL

...
# PR Readiness

PR_READY | PR_NOT_READY

missing inputs:
- grounded summary available:
- validation summary available:
- remaining risks available:
- Required Fixes Before Closer
...
# Residual Risks
...
```
