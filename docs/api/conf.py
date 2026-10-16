"""Sphinx frontend for the native Doxygen XML reference."""
from pathlib import Path

project = "SharpTestBench"
extensions = ["breathe"]
breathe_projects = {"SharpTestBench": str(Path(__file__).resolve().parents[2] / "build/docs/doxygen/xml")}
breathe_default_project = "SharpTestBench"
html_theme = "alabaster"
exclude_patterns = []
# abi.h is intentionally consumable as C11; render its typedefs in the C domain.
breathe_domain_by_file_pattern = {"*abi.h": "c"}
