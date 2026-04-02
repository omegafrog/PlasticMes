#!/usr/bin/env python3
from __future__ import annotations

import subprocess
import sys
from datetime import date
from pathlib import Path
import re


SCRIPT_DIR = Path(__file__).resolve().parent
ROOT = SCRIPT_DIR.parents[3]
DOCS = ROOT / "docs"
VALIDATOR = ROOT / "scripts" / "validate_docs.py"
LINK_RE = re.compile(r"\[([^\]]+)\]\(([^)]+)\)")
RELATED_DOC_NAMES = {
    "domain-boundary.md",
    "use-cases.md",
    "event-storming.md",
    "detailed-design.md",
}


def format_failures(stdout: str) -> dict[str, list[str]]:
    failures: dict[str, list[str]] = {}
    for line in stdout.splitlines():
        if not line.startswith("ERROR: "):
            continue
        payload = line[len("ERROR: ") :]
        file_part, _, rest = payload.partition(":")
        target = file_part or "<unknown file>"
        message = rest.strip() or "<missing message>"
        failures.setdefault(target, []).append(message)
    return failures


def collect_verification_targets() -> list[Path]:
    targets: list[Path] = []
    seen: set[Path] = set()

    for plan_path in sorted(DOCS.glob("exec-plans/active/**/plan.md")):
        if plan_path not in seen:
            seen.add(plan_path)
            targets.append(plan_path)

        text = plan_path.read_text(encoding="utf-8")
        for _, raw_target in LINK_RE.findall(text):
            if raw_target.startswith("http://") or raw_target.startswith("https://"):
                continue

            resolved = (plan_path.parent / raw_target).resolve()
            if not resolved.is_file():
                continue
            if resolved.suffix != ".md":
                continue
            if resolved.name not in RELATED_DOC_NAMES:
                continue

            try:
                resolved.relative_to(DOCS)
            except ValueError:
                continue

            if resolved not in seen:
                seen.add(resolved)
                targets.append(resolved)

    return targets


def update_verification_metadata(doc_path: Path, verified_on: str) -> bool:
    original = doc_path.read_text(encoding="utf-8")
    updated = original

    if "# Properties" not in updated:
        updated = (
            "# Properties\n\n"
            "status: verified\n\n"
            f"last_verified: {verified_on}\n\n"
            f"{updated.lstrip()}"
        )
    else:
        if re.search(r"(?m)^status:\s*.*$", updated):
            updated = re.sub(r"(?m)^status:\s*.*$", "status: verified", updated, count=1)
        else:
            updated = re.sub(
                r"(?m)^# Properties\s*$",
                "# Properties\n\nstatus: verified",
                updated,
                count=1,
            )

        if re.search(r"(?m)^last_verified:\s*.*$", updated):
            updated = re.sub(
                r"(?m)^last_verified:\s*.*$",
                f"last_verified: {verified_on}",
                updated,
                count=1,
            )
        else:
            updated = re.sub(
                r"(?m)^status:\s*.*$",
                lambda match: f"{match.group(0)}\n\nlast_verified: {verified_on}",
                updated,
                count=1,
            )

    if updated == original:
        return False

    doc_path.write_text(updated, encoding="utf-8")
    return True


def main() -> int:
    if not VALIDATOR.exists():
        print(f"ERROR: validator script not found at {VALIDATOR}", file=sys.stderr)
        return 1

    proc = subprocess.run(
        [sys.executable, str(VALIDATOR)],
        cwd=ROOT,
        capture_output=True,
        text=True,
    )

    if proc.stdout:
        print(proc.stdout, end="")
    if proc.stderr:
        print(proc.stderr, file=sys.stderr, end="")

    failures = format_failures(proc.stdout)
    if not failures and proc.returncode == 0:
        verified_on = date.today().isoformat()
        updated_docs = [
            path for path in collect_verification_targets()
            if update_verification_metadata(path, verified_on)
        ]
        print("\nPASS: documentation structure validated")
        if updated_docs:
            print(f"Updated verification metadata to {verified_on}:")
            for path in updated_docs:
                print(f"- {path.relative_to(ROOT)}")
    elif not failures:
        print("\nPASS: validator exited non-zero but did not print ERROR lines; review output above.")
    else:
        print("\nFAIL: documentation validator reported issues")
        for plan_path in sorted(failures):
            print(f"- {plan_path}:")
            for reason in failures[plan_path]:
                print(f"    • {reason}")

    return proc.returncode


if __name__ == "__main__":
    sys.exit(main())
