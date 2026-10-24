"""StrictDoc-required configuration; batch automation remains C#/PowerShell.

Explicit file relations provide portable whole-file C# implementation and test
traceability. Generated build output is not treated as source requirements.
"""
from pathlib import Path
from strictdoc.core.project_config import ProjectConfig


def create_config() -> ProjectConfig:
    return ProjectConfig(
        project_title="SharpTestBench requirements",
        project_features=["REQUIREMENT_TO_SOURCE_TRACEABILITY"],
        source_root_path=str(Path(__file__).resolve().parents[2]),
        include_source_paths=["src/SharpTestBench/**", "tests/SharpTestBench.Tests/Selection*.cs", "tests/SharpTestBench.Tools.Tests/Selection*.cs", "tests/ProtocolContracts/**", "tests/SelectionQuality/**", "tools/SharpTestBench.Tools/*AuditCommand.cs", "tools/SharpTestBench.Tools/SelectionCiCommand.cs"],
        exclude_source_paths=["**/bin/**", "**/obj/**"],
    )
