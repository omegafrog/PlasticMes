---
name: clarify-request
description: "Before planning starts, normalize the user's request into a tight handoff package by resolving missing scope, assumptions, blockers, and non-goals."
---

# Clarify Request

Act as a short pre-planning gate for plan-driven work.

Your job is to reduce avoidable ambiguity before `oracle` starts planning.
Do not create a full implementation plan.
Do not write code.
Do not write product or design docs.
Do not expand the task beyond what the user asked for.

## When To Use

Use this step immediately before `oracle` when a request is likely to produce a wrong or unstable plan because the task is underspecified.

Typical triggers:

- Missing acceptance criteria
- Missing system boundary or target component
- Conflicting constraints
- Unclear in-scope versus out-of-scope work
- Missing environment, data, or dependency assumptions
- User intent that is broad enough to support multiple materially different plans

If the request is already precise enough to plan safely, do not create artificial friction. Produce a pass-through handoff package with minimal normalization and no new questions.

## Workflow

1. Read the user's request and identify only the ambiguities that can materially change planning or execution.
2. Ask at most 3 short clarification questions only if the missing information is necessary.
3. If you can proceed safely with explicit assumptions, prefer assumptions over unnecessary questioning.
4. Normalize the result into a handoff package for `oracle`.
5. Keep the package concise, factual, and directly reusable by the next stage.

## Output Contract

Always return these sections in this order:

1. `Clarification Status`
   - `PASS_THROUGH` when the original request is already plan-ready
   - `CLARIFIED` when missing details were resolved or explicit assumptions were added
   - `BLOCKED` when planning cannot proceed safely without user input
2. `Confirmed Scope`
3. `Explicit Assumptions`
4. `Open Questions`
5. `Non-Goals`
6. `Oracle Handoff`

### Section Rules

- `Confirmed Scope` must restate only what is in scope for planning.
- `Explicit Assumptions` must contain only assumptions you are intentionally making so `oracle` does not silently invent them.
- `Open Questions` must be empty when planning can safely proceed.
- `Non-Goals` must identify nearby work that should not be pulled into the plan.
- `Oracle Handoff` must be a compact, self-contained task package that `oracle` can plan from directly.

## Constraints

- Do not generate more than 3 clarification questions.
- Do not ask questions whose answers can be safely expressed as explicit assumptions.
- Do not produce architecture, task breakdowns, milestone lists, or implementation steps.
- Do not remove user-stated constraints.
- Do not hide uncertainty inside prose; put it under `Explicit Assumptions` or `Open Questions`.
- When blocked, state exactly why planning would be unsafe.

## Response Style

- Keep the output short and structured.
- Prefer bullets over paragraphs.
- Preserve the user's terminology when possible.
- Make assumptions concrete enough that a later verifier can check whether they were honored.
