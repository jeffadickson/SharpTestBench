#!/usr/bin/env python3
"""Repeated Icarus process teardown plus an expected-exception leak negative test.

Build Integration with run.py first. Uses the same CS_* tool overrides.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repeat", type=int, default=20)
    args = parser.parse_args()
    if args.repeat < 1:
        parser.error("--repeat must be positive")
    reports = []
    for index in range(args.repeat + 2):
        negative = index == args.repeat
        early = index == args.repeat + 1
        label = f"lifecycle-{index:03d}"
        env = os.environ.copy()
        env.pop("CS_INJECT_FAILURE", None)
        env.pop("CS_INJECT_LEAK", None)
        env.pop("CS_INJECT_EARLY", None)
        if negative:
            env["CS_INJECT_LEAK"] = "1"
        if early:
            env["CS_INJECT_EARLY"] = "1"
        command = [sys.executable, str(ROOT / "tools/run.py"), "--no-build",
                   "--filter", "Z_Leak" if negative else "Y_SameTime", "--label", label]
        run = subprocess.run(command, env=env, text=True, capture_output=True, timeout=180)
        output = run.stdout + run.stderr
        (ROOT / f"build/{label}-driver.log").write_text(output)
        path = ROOT / f"build/{label}-results.json"
        results = json.loads(path.read_text()) if path.exists() else []
        diagnostic = "fatal library error" in output.lower()
        if early:
            passed = (run.returncode == 1 and any(r["Name"] == "simulator-termination"
                      and r["Status"] == "failed" for r in results))
        elif negative:
            passed = (run.returncode == 1 and len(results) == 1
                      and results[0]["Status"] == "failed"
                      and "Test leaked tasks or callbacks" in results[0]["Error"])
        else:
            passed = (run.returncode == 0 and len(results) == 1
                      and results[0]["Status"] == "passed")
        reports.append(dict(label=label, negative=negative, early=early, exit_code=run.returncode,
                            fatal_diagnostic=diagnostic, passed=passed and not diagnostic))
    target = ROOT / "build/lifecycle-summary.json"
    target.write_text(json.dumps(reports, indent=2) + "\n")
    failed = sum(not r["passed"] for r in reports)
    print(f"LIFECYCLE {len(reports) - failed} passed {failed} failed; {target}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
