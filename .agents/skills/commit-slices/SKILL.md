---
name: commit-slices
description: Break implementation work into small, reviewable commit slices during execution. Use this inside execute when a task spans multiple files or multiple plan checklist items.
---

## Purpose

1. Keep implementation changes small, reviewable, and reversible.
2. Prevent one large mixed commit from hiding scope creep or broken intermediate states.
3. Align commits with plan checklist items and architectural boundaries.
4. Make later documentation sync and closure easier to audit.

## When to use

- During `execute` when implementing more than one checklist item.
- When a task touches multiple modules, packages, adapters, DTOs, or tests.
- When the user or workflow expects traceable intermediate commits.
- Do not use for tiny one-file typo-level changes unless the repo policy explicitly requires it.

## Commit Slicing Rules

### 1. Slice by plan item first

Prefer one commit per smallest meaningful Implementation Plan item.

Good examples:

- add command DTO and validator
- add application service method
- add repository adapter
- add controller endpoint
- add integration test
- update configuration wiring

Avoid combining unrelated checklist items in one commit.

### 2. Slice by architectural boundary second

If one plan item spans multiple layers, split by boundary when useful:

- domain model change
- application service / use case orchestration
- adapter / infrastructure wiring
- API contract / controller
- test coverage

### 3. Keep each commit coherent

A commit should answer one question:
“What single implementation step did this introduce?”

If the answer requires “and also” more than once, split again.

### 4. Preserve buildability when practical

Before each commit, prefer to ensure the changed code is at least locally coherent.

Try to avoid commits that leave:

- uncompilable code
- broken imports
- dead references
- half-renamed types

If a temporary broken intermediate step is unavoidable, keep it local and do not commit it.

### 5. Pair code and tests intentionally

Preferred order:

- production code commit
- test commit
or
- single combined commit if the change is tiny and inseparable

Do not bury major test strategy changes inside unrelated code commits.

### 6. Separate mechanical changes

If formatting, rename, or file move is unavoidable and large:

- isolate it in its own commit
- do not mix it with behavior changes

### 7. Record doc-impact boundaries

When a commit introduces a doc-relevant change, note it in the execution report:

- new public interface
- new config
- new env var
- new event or DTO
- responsibility move
- new file or renamed file

## Pre-Commit Checklist

Before creating a commit, check:

1. Is this commit tied to exactly one small implementation step?
2. Does it map to a checklist item in `plan.md`?
3. Does it avoid unrelated refactoring?
4. Are imports, references, and file structure coherent?
5. Is there a clear commit message?
6. Should this step be documented later?

If any answer is “no”, split or revise before committing.

## Commit Message Format

Use one of these formats:

- `feat(<area>): <small implementation step>`
- `fix(<area>): <small implementation step>`
- `refactor(<area>): <scoped internal cleanup>`
- `test(<area>): <test coverage step>`
- `chore(<area>): <scoped non-functional change>`

Examples:

- `feat(auth): add social login start command handler`
- `feat(auth): wire oauth redirect adapter`
- `test(auth): cover social login callback failure cases`
- `refactor(queue): isolate waiting rank calculator`

## Output Notes for Execute

When this skill is used, `execute` should include in its final output:

- commit grouping by plan checklist item
- which files changed in each slice
- whether each slice had doc impact
- whether validation was run before or after the slice

## Do Not

- Do not create one giant commit for the whole task.
- Do not split purely to maximize commit count.
- Do not mix unrelated modules in one commit.
- Do not hide design deviations inside a later cleanup commit.
- Do not commit broken intermediate states unless the repo explicitly permits it.
