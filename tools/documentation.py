"""Build native reference and validate C# XML docs without requiring a simulator."""
from __future__ import annotations

import argparse
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PROJECTS = ("SharpTestBench", "SharpTestBench.Generators")


def managed(dotnet: str) -> None:
    """Reject new undocumented library symbols and malformed compiler XML."""
    missing = set()
    for name in PROJECTS:
        result = subprocess.run(
            [dotnet, "build", str(ROOT / "src" / name), "-c", "Release", "-t:Rebuild",
             "-m:1", "/nodeReuse:false", "-p:RestoreLockedMode=true"],
            cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        log = ROOT / "build/docs" / f"{name}.log"
        log.parent.mkdir(parents=True, exist_ok=True)
        log.write_text(result.stdout, encoding="utf-8")
        if result.returncode:
            raise RuntimeError(f"Managed documentation build failed; see {log}")
        missing.update(re.findall(r"warning CS1591: Missing XML comment for publicly visible type or member '([^']+)'", result.stdout))
        framework = "net10.0" if name == "SharpTestBench" else "netstandard2.0"
        xml = ROOT / "src" / name / "bin/Release" / framework / f"{name}.xml"
        document = ET.parse(xml)
        if not document.findall("./members/member"):
            raise RuntimeError(f"Empty API documentation: {xml}")
    baseline = set((ROOT / "docs/api/csharp-undocumented.txt").read_text().splitlines())
    added = missing - baseline
    if added:
        raise RuntimeError("Add XML comments for new public APIs:\n" + "\n".join(sorted(added)))
    print(f"C# XML valid; {len(missing)} existing undocumented public members; {len(baseline - missing)} backlog entries resolved.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET", "dotnet"))
    parser.add_argument("--managed-only", action="store_true")
    args = parser.parse_args()
    managed(args.dotnet)
    if not args.managed_only:
        subprocess.run(["doxygen", "docs/api/Doxyfile"], cwd=ROOT, check=True)
        subprocess.run([sys.executable, "-m", "sphinx", "-W", "--keep-going", "-b", "html",
                        "docs/api", "build/docs/html"], cwd=ROOT, check=True)


if __name__ == "__main__":
    main()
