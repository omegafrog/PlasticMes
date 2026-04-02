---
name: docs-verify
description: Run the repository's docs validator whenever planning docs are created or updated; use this after plan-writer output or any docs under docs/exec-plans or docs/design-docs change to confirm structure, required headers, and relative link integrity.
---

## Purpose

1. Run the repo's existing documentation validator command so you stay within the same checks as the humans who maintain the repo.
2. Surface a clear pass/fail verdict plus the exact plan files and rules that failed so the user can fix them.
3. When validation succeeds, mark the plan and its related planning docs as `verified` and stamp `last_verified` with the successful run date.

## When to use

- Right after plan-writer creates or updates `docs/exec-plans/**/plan.md` and related planning docs (`domain-boundary.md`, `use-cases.md`, `event-storming.md`, `detailed-design.md`).
- When the user asks explicitly to validate planning documents or mentions broken links, missing plan headers, or status tags under `docs/exec-plans` or `docs/design-docs`.
- Anytime you touched documentation structure in `docs/exec-plans` or `docs/design-docs` and want to confirm everything passes the repo checks before proceeding.

## Invocation

1. Run the script that lives under this skill so you always call the same validator command that the maintainers expect:

```
python3 .agents/skills/docs-verify/scripts/run.py
```

2. The script executes `scripts/validate_docs.py` from the repo root, prints whatever the validator prints, and then adds a summarized pass/fail block showing each affected plan file and the rule(s) that failed.
3. If validation succeeds, the script updates the active `plan.md` files and each plan's linked `domain-boundary.md`, `use-cases.md`, `event-storming.md`, and `detailed-design.md` files so their metadata shows `status: verified` and `last_verified: YYYY-MM-DD`.
4. If the validator ever exits with a non-zero code, this script exits non-zero as well, so Codex knows the check failed and can avoid pretending the docs are clean.

## Output handling

- On success you will see `PASS:` plus a confirmation that structure, headers, and uses of required docs are intact, followed by the docs whose verification metadata was updated.
- On failure the script lists every `plan.md` file with missing headers, invalid status tags, or broken relative links and prints a short summary per file. Use that summary to guide your next edit before rerunning the validator.
