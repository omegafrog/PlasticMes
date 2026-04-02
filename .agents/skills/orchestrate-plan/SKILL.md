---
name: orchestrate-plan
description: "Orchestrate the full delegated workflow `clarify-request -> oracle -> plan-writer -> plan-doc-verify -> execute -> execute-writer -> docs-sync-verify -> closer` for plan-driven tasks."
---

# Orchestrate Plan

Act primarily as an orchestrator for delegated plan-to-execution workflows.

Your job is to manage handoffs between specialist agents and verifier skills.
Do not analyze the task yourself.
Do not create plans yourself.
Do not implement code yourself.
Do not write product/design content yourself.
Do not substitute your own reasoning for delegated planning, implementation, documentation, verification, or closure work.

## Workflow

1. Run the `clarify-request` skill first.
2. If the request is already precise enough, allow a short pass-through result with no new user questions.
3. If material ambiguity remains, resolve it through the narrow clarification output contract before planning starts.
4. If `clarify-request` returns `BLOCKED`, stop and report the blocker to the user.

5. Invoke the `oracle` agent second.
6. Pass only the final `Oracle Handoff` output from `clarify-request` to `oracle`.
7. Wait for `oracle` to return its full output.

8. Invoke the `plan-writer` agent third.
9. Pass `oracle`'s full output to `plan-writer` unchanged.
10. Wait for `plan-writer` to finish creating or updating the planning docs.

11. Run the `plan-doc-verify` skill fourth.
12. Validate that the generated planning docs fully preserve the oracle output and also pass repository docs structure checks.
13. If `plan-doc-verify` fails, send the latest validation output plus the latest raw `oracle` output to `plan-writer`, scoped only to the reported planning-doc issues.
14. Wait for `plan-writer` to finish the follow-up planning doc update.
15. Re-run `plan-doc-verify`.
16. Repeat the `plan-writer -> plan-doc-verify` loop until the planning docs pass or you hit a blocker that requires the user.

17. Invoke the `execute` agent fifth. If the user says "executor", map that request to the `execute` agent type.
18. Pass the final `plan-writer` output to `execute` unchanged.
19. Wait for `execute` to finish and capture its full output, including the implementation report and changed file paths.

20. Invoke the `execute-writer` agent sixth.
21. Pass `execute`'s full output to `execute-writer` unchanged.
22. Wait for `execute-writer` to finish updating the related docs.

23. Run the `docs-sync-verify` skill seventh.
24. Validate that the docs now reflect the real implementation, the execute report, and the current diff, and that repository docs structure checks still pass.
25. If `docs-sync-verify` fails, send the latest sync-validation output plus the latest raw `execute` output to `execute-writer`, scoped only to the reported doc-sync issues.
26. Wait for `execute-writer` to finish the follow-up doc update.
27. Re-run `docs-sync-verify`.
28. Repeat the `execute-writer -> docs-sync-verify` loop until the docs pass or you hit a blocker that requires the user.

29. Invoke the `closer` agent last.
30. Pass the final `plan-doc-verify` result, the final `docs-sync-verify` result, and the latest closure-relevant task context to `closer`.
31. Wait for `closer` to complete:

- active -> completed transition
- completion metadata update
- PR creation or PR draft generation
32. Report the delegated outputs and final closure status back to the user.

## Delegation Rules

- Do not fork the current conversation context into delegated agents. Use `fork_context: false`.
- Do not pass the `$orchestrate-plan` trigger, the skill body, or any other orchestration/meta instructions to delegated agents.
- Run `clarify-request` before `oracle` for plan-driven work; do not skip it just because the request looks familiar.
- Preserve the user's original request as much as possible when feeding it into `clarify-request`.
- Pass only the final `Oracle Handoff` package from `clarify-request` to `oracle`.
- Preserve `oracle`'s original output exactly when delegating to `plan-writer`.
- Preserve the final `plan-writer` output exactly when delegating to `execute`.
- Preserve `execute`'s original output exactly when delegating to `execute-writer`.
- Do not rewrite, reinterpret, summarize, enrich, or filter delegated outputs before handing them to the next delegated stage unless the workflow explicitly requires adding verifier output on a retry or extracting the final `Oracle Handoff` section from `clarify-request`.
- Do not skip `clarify-request`, `oracle`, `plan-writer`, `plan-doc-verify`, `execute`, `execute-writer`, `docs-sync-verify`, or `closer` when this workflow is in scope.
- Do not replace any delegated planning, implementation, doc-writing, verification, or closure step with your own reasoning or writing.
- If `clarify-request` asks the user questions, keep them short and limited to material ambiguities.
- If `clarify-request` can safely proceed with explicit assumptions, prefer that over blocking the workflow.
- If `clarify-request` returns `BLOCKED`, do not invoke `oracle`.
- After `plan-writer` finishes, you must run `plan-doc-verify`; do not proceed to `execute` before planning docs pass.
- After `execute-writer` finishes, you must run `docs-sync-verify`; do not proceed to `closer` before documentation sync passes.
- Validation fixes must stay narrow:
  - `plan-doc-verify` retries are limited to planning-doc completeness or structure issues.
  - `docs-sync-verify` retries are limited to code-to-doc sync issues or structure issues.
- Prefer re-invoking `plan-writer` for planning-doc failures and `execute-writer` for post-execution doc-sync failures.
- Use local edits only for trivial structural repairs when another delegation adds no value.
- If a verifier keeps failing, continue the narrow fix-and-verify loop until it passes or the remaining issue is a real ambiguity or blocker.
- If any delegated agent or verifier fails, report that failure plainly instead of substituting your own content.
- If validation cannot be made to pass without inventing missing product, design, or implementation content, stop and report the specific blocker instead of fabricating it.

## Invocation Shape

- `clarify-request` should receive a plain task message only.
- `oracle` should receive only the final `Oracle Handoff` section from `clarify-request`.
- `plan-writer` should receive the raw `oracle` output only.
- `plan-doc-verify` should run against the generated planning docs plus the raw `oracle` output.
- On `plan-doc-verify` retries, `plan-writer` should receive the latest raw `oracle` output plus the latest `plan-doc-verify` failure output, with instructions limited to fixing reported planning-doc issues.
- `execute` should receive the final raw `plan-writer` output only.
- `execute-writer` should receive the raw `execute` output only on the first pass.
- On `docs-sync-verify` retries, `execute-writer` should receive the latest raw `execute` output plus the latest `docs-sync-verify` failure output, with instructions limited to fixing reported doc-sync issues.
- `closer` should receive:
  - the final `plan-doc-verify` PASS result
  - the final `docs-sync-verify` PASS result
  - the latest execution summary
  - the latest changed-file context needed for completion and PR creation
- If the user invoked this skill explicitly, strip the trigger token from the delegated message and keep only the underlying task request.

## Response Style

- Keep the orchestration response short and factual.
- State clearly whether `clarify-request` passed through, clarified assumptions, or blocked.
- State clearly that `oracle` was invoked after clarification completed.
- State clearly that `plan-writer` was invoked after `oracle`.
- State clearly whether `plan-doc-verify` passed before execution.
- State clearly that `execute` was invoked after planning verification passed.
- State clearly that `execute-writer` was invoked after implementation.
- State clearly whether `docs-sync-verify` passed before closure.
- State clearly whether `closer` completed:
  - the active-to-completed transition
  - the PR creation or PR draft generation
- If validation required follow-up edits, report that those edits were limited to validator-driven fixes.
