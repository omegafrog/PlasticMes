from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"

ALLOWED_STATUS = {"draft", "verified", "deprecated"}

LINK_RE = re.compile(r"\[([^\]]+)\]\(([^)]+)\)")


def fail(msg: str) -> None:
    print(f"ERROR: {msg}")


def check_plan(plan_path: Path) -> list[str]:
    errors: list[str] = []
    text = plan_path.read_text(encoding="utf-8")

    if "# Properties" not in text:
        errors.append(f"{plan_path}: missing '# Properties'")

    for field in ["owner:", "status:", "title:", "parent_docs:"]:
        if field not in text:
            errors.append(f"{plan_path}: missing '{field}'")

    m = re.search(r"status:\s*([A-Za-z0-9_-]+)", text)
    if not m:
        errors.append(f"{plan_path}: status value not found")
    elif m.group(1) not in ALLOWED_STATUS:
        errors.append(f"{plan_path}: invalid status '{m.group(1)}'")

    for _, raw_target in LINK_RE.findall(text):
        if raw_target.startswith("http://") or raw_target.startswith("https://"):
            continue
        target = (plan_path.parent / raw_target).resolve()
        if not target.exists():
            errors.append(f"{plan_path}: broken link -> {raw_target}")

    required_names = [
        "domain-boundary.md",
        "use-cases.md",
        "event-storming.md",
        "detailed-design.md",
    ]
    for name in required_names:
        if name not in text:
            errors.append(f"{plan_path}: missing reference to {name}")

    return errors


def main() -> int:
    plan_files = list(DOCS.glob("exec-plans/active/**/plan.md"))
    if not plan_files:
        fail("no plan.md files found under docs/exec-plans/active")
        return 1

    errors: list[str] = []
    for plan in plan_files:
        errors.extend(check_plan(plan))

    if errors:
        for e in errors:
            fail(e)
        print(f"\nFAILED: {len(errors)} issue(s)")
        return 1

    print("OK: documentation structure validated")
    return 0


if __name__ == "__main__":
    sys.exit(main())
