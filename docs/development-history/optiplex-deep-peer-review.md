# Independent deep review: OptiPlex parity environment fixes

Review date: 2026-10-20. One independent reviewer performed multiple read-only passes over the focused two-defect batch. No actionable P0/P1/P2/P3 findings remain. This is source review, not independently executed validation.

## Scope

Reviewed ParityContracts.ChildEnvironment, its use in ParityCiCommand and ManifestCheckCommand, LegacyRunCommand.SimulatorEnvironment, all three ParityEnvironmentTests, and the DUAL_RUNNER_PARITY.md corrections. Also inspected the existing f19e028 production Runner coverage implementation: RunnerValidation.csproj and protocol entry point, CoverageCommand, Directory.Build.props and CODE_COVERAGE.md.

## Process inheritance

CommandRunner creates ProcessStartInfo with the parent environment and then applies its supplied dictionary as overrides. Removing a variable from that dictionary leaves its inherited value present. ChildEnvironment correctly supplies explicit null overrides for every control in SelectionVariables. Fixture preparation then assigns its own values; left/right copies retain removal overrides and receive separate report destinations. Nested simulator processes therefore inherit the intended sanitized state. ManifestCheckCommand retains its prior removal behavior through the shared helper. CleanEnvironment preserves its separate dictionary-filtering contract for the retained test API.

## Windows and Unix loader paths

The source runner previously copied inherited variables into an ordinal dictionary. Inherited Path was invisible to a lookup for PATH. Constructing uppercase PATH could then replace the effective Windows search path with only host/adapter directories, losing directories needed by dependent DLLs. The new Windows OrdinalIgnoreCase dictionary finds and preserves the inherited value, and updating the existing entry avoids competing case variants.

Unix retains ordinal comparison and case-sensitive environment semantics. Linux now prefixes LD_LIBRARY_PATH once; the old code prefixed it twice because LoaderPath was also LD_LIBRARY_PATH. macOS continues to prefix both LD_LIBRARY_PATH and DYLD_LIBRARY_PATH. The inherited dictionary is copied before modification. Existing trailing-separator behavior for an absent loader path is unchanged.

## Regression quality

The process test poisons every listed control, launches a real child through CommandRunner, verifies removal, confirms an explicit DSP_FAULT override survives and checks inherited PATH preservation. Parent values are restored in finally. The nonparallel collection appropriately isolates process-global environment mutation from other xUnit collections. It requires the selected CS_PYTHON interpreter or python on PATH; full simulator qualification remains separate evidence.

The dictionary test protects the retained filtering contract, unrelated values and input immutability. The simulator-environment test exercises Windows Path casing, platform loader-path preservation, unrelated values, input immutability and a single effective loader key. The final Assert.Single predicate overload preserves its assertion while satisfying the analyzer.

## Schemas and existing coverage implementation

No manifest fields, parity comparison rules, expected identities, seed checks, fault-marker requirements, exit-code classification or failure-report structures changed. The fixes control which intended simulation executes without weakening its evidence rules.

RunnerValidation still references the production Runner project rather than linked source copies. CoverageCommand still separately collects retained test_*runner*.py contracts and requires all four production assemblies in the merged report. EmitCompilerGeneratedFiles remains enabled, preserving generated source documents for report presentation. The batch preserves f19e028's implementation and scope.

These are managed contract measurements, not production CLI entry-point/runtime-switch execution, NativeAOT instruction coverage, HDL coverage or simulator-only behavior. Generated code remains in the denominator. The documentation corrections agree with inspected source. Workflow trigger definitions remain present; the disabled remote state was supplied by the primary agent and was not independently inspected by this reviewer.

## Limits

The reviewer did not build, execute tests/simulations, collect coverage, qualify publication, modify files or inspect remote state. The Windows reproduction and final execution results belong to the primary agent's separate validation report. No additional platform qualification follows from this source review.
