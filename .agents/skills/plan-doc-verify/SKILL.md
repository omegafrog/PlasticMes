---
name: plan-doc-verify
description: "Verify that oracle output was fully materialized into plan.md and related planning documents after plan-writer runs, before execution begins."
---

## Purpose

1. Verify that the raw `oracle` output was not partially lost when `plan-writer` split it into `plan.md` and linked planning documents.
2. Confirm that all required planning sections exist, are reachable from `plan.md`, and preserve the intended planning structure.
3. Detect missing use cases, missing event/policy/command traceability, missing detailed design elements, and missing documentation targets before implementation starts.
4. Reuse the repository docs validator so planning docs must be both complete and structurally valid before execution.

## When to use

- Immediately after `plan-writer` creates or updates planning documents.
- Before `execute` starts implementation.
- Whenever the user asks whether the generated planning docs are complete, not just structurally valid.

## Required Inputs

Inspect all of the following in this order:

1. `AGENTS.md`
2. Raw `oracle` output
3. Generated `plan.md`
4. Documents linked from `plan.md`
   - `domain-boundary.md`
   - `use-cases.md`
   - `event-storming.md`
   - `detailed-design.md`
5. Repository docs validator result from:

```bash
python3 .agents/skills/docs-verify/scripts/run.py
```

## Verification Rules

### 1. Entry Point Integrity

plan.md must exist.
plan.md must act as the single entry point for the task.
All planning docs must be linked from plan.md using relative paths.
plan.md must clearly identify the task title, status, owner, parent docs if available, domain, and last verification metadata when present.

### 2. Required Section Preservation

The oracle output must still be represented after document splitting.

The following sections must exist either in plan.md or in linked docs:

Task Summary
Domain Boundary
Use Cases
Event Storming
Detailed Design
Implementation Plan
Verification Plan
Documentation Plan
Output Files
Assumption if present in the oracle output
Out of Scope if present in the oracle output

### 3. Correct Section Placement

Verify the split destination is correct:

Domain Boundary → product-spec document
Use Cases → product-spec document
Event Storming → design document
Detailed Design → design document
Implementation Plan → plan.md
Verification Plan → plan.md
Documentation Plan → plan.md
Output Files → plan.md

### 4. Use Case Integrity

Every use case ID must match UC-XXX.
Every use case from oracle output must appear in the docs.
No use case may disappear during splitting.
Actor, action, and use-case intent must remain identifiable.
If multiple use cases exist, all must remain distinguishable.

### 5. Event Storming Traceability

Every event must reference at least one related use case ID.
Every policy must reference at least one related use case ID.
Every command must reference at least one related use case ID.
If oracle included an event, policy, or command and it is absent from docs, report it as missing.
If docs add new events, policies, or commands that did not come from oracle, report them as unexpected unless clearly marked as editorial structure only.

### 6. Detailed Design Integrity

The detailed design must still include:

ports
adapters
interface signatures
key DTOs
test points

If oracle output included explicit UI, infrastructure, adapter, or contract responsibilities, they must remain represented.

### 7. Implementation and Verification Integrity

Implementation Plan must remain checklist-oriented and actionable.
Verification Plan must include execution commands, expected results, and fallback or redesign points on failure.
The plan must still be suitable to hand off directly to execute.

### 8. Documentation Plan Integrity

Documentation targets promised by oracle must be explicitly listed.
Created or updated document paths must be identifiable.
Output files listed by oracle must remain represented.
If the documentation plan references specific doc families, those families must still be visible in the generated plan set.

### 9. Structural Validation

Run:

```python
python3 .agents/skills/docs-verify/scripts/run.py
```

Do not replace validator output with guesswork.
A structurally valid plan that lost required planning content must still fail this skill.

## Decision Policy

- Mark PASS only if both completeness and structure checks pass.
- Mark FAIL if any required section, traceability item, placement rule, or validator rule fails.
- Do not invent missing content.
- Do not rewrite documents.
- Do not assume omitted content is implied.
- If wording changed but meaning is clearly preserved, mark it as represented.
- If preservation is uncertain, mark it as unclear.

## Output Format

```md
# Verdict

PASS | FAIL

# Missing Sections
...
# Incorrect Section Placement
...
# Missing Use Cases
...
# Missing Event Storming Traceability
...
# Missing Detailed Design Elements
...
# Structural Validation

PASS | FAIL

...
# Oracle-to-Docs Mapping
represented:
missing:
unclear:
# Required Fixes Before Execute
...
# Handoff Readiness

READY | NOT_READY

# Notes
...

```
