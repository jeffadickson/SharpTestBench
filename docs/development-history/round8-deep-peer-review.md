# Final deep peer reviews: SharpTestBench round8

Three independent read-only source reviews found no unresolved high/medium correctness defects. Final validation, coverage and remote delivery are in round8-validation-report.md and round8-delivery.json. Scheduled review history is in round8-reviews.jsonl.

---

# Final independent migration and native deployment review

Review point: iteration 200 of round 8. Reviewer performed read-only inspection of source, current tests, and the cumulative changes from eb9e2f6. Build/test checkpoint 200 was still running when this report was written; final validation status must come from its completed evidence. No source edits were made by this reviewer.

## Findings

No unresolved P0/P1/P2 correctness defect found in the reviewed final source. Earlier review findings were corrected: malformed BOM literal, unsuccessful Runner help probe, missed managed child invocation, publish-directory symlink escape, concurrent Make native builds, and stale Release contract assemblies under Debug test configuration.

## Architecture and migration inventory

Repository-owned helper orchestration resides in tools/SharpTestBench.Tools. The standalone Icarus orchestrator resides in tools/SharpTestBench.Runner. Tracked production Python helper implementations under tools are absent. The current inventory supplied by the root reviewer identifies 55 C# helper source files and 45 retained Python files; verify these counts against the final committed inventory before publishing exact totals.

Retained Python serves independent cocotb oracle/scoreboard tests, contract RPC bindings, benchmark reference execution, and Sphinx configuration. C# also deliberately embeds narrow Python adapters for upstream APIs: OracleFixturesCommand owns a Python object bridge, VerilatorBuildCommand calls the upstream builder, CocotbAdapter queries installed upstream configuration, and AuditCommand uses Python AST parsing. This is a C# orchestration migration, not a Python-free system.

## Native execution model

Full Tools publishes as directory-based self-contained ReadyToRun with a bundled .NET runtime and JIT fallback. It is not NativeAOT. Runner publishes through NativeAOT with reflection-free generated JSON metadata. Native Runner orchestration can start without an installed .NET runtime; managed simulator test assemblies still execute through hostfxr in the simulator process. Native compiler/linker, simulator, native bridge and GPI dependencies remain applicable.

Publishing is limited to matching host RID across six advertised x64/arm64 Windows/Linux/macOS build plans. A supported plan is not evidence that all six platforms were executed. Parent-reported functional evidence includes Linux NativeAOT simulation success and classified failure, published Tools child invocation, runtime-independent help startup, ReadyToRun header validation, and Windows ReadyToRun publishing. Final delivery must report only completed gates.

## Publish paths and artifact integrity

WorkerPublishCommand requires outputs below the checkout build directory, resolves output containment, rejects linked build/RID/worker components before publishing, and uses argument arrays rather than shell command interpolation. WorkerArtifact records exact relative file sets and SHA-256 hashes; verification rejects altered/missing/extra files and identity mismatches. StrictJson rejects duplicate keys recursively. Supplemental tests cover malformed file maps, numeric hashes, unknown fields and duplicate root/file keys. Unix tests cover output/RID/leaf redirects and artifact root/file/directory links, with honest Windows skips.

Checksums establish consistency against the local manifest, not authenticity or a signed software supply chain. A party able to replace both manifest and files can recompute valid hashes. Output directories should remain task-owned and quiescent during publish and verification; concurrent hostile filesystem replacement is not prevented by these path checks.

## Serialization compatibility

Generated manifest context retains root property case-insensitivity and unknown-field rejection, while nested define dictionaries preserve distinct FOO/foo keys. Tests cover zero, ordinary seeds, 2^32, 2^53+1, 2^63 and UInt64 maximum through parse/serialize/parse. The explicit BOM escape avoids source encoding corruption. Typed evidence records preserve previous property names, arrays, null process fields, nested drain counters, and DateTimeOffset serialization. JsonElement evidence writing avoids reflection-dependent serialization in the AOT entry point.

## Make interface

Root and tools Makefiles expose local build, all four test suites, coverage, native publication, verification, and direct execution. Native all-mode uses one ordered recipe; test and coverage intentionally build Release to match retained contract harness paths. LF attributes are explicit. Arbitrary simultaneous independent make goals can still contend over ordinary shared build outputs, as with conventional SDK projects; run coverage instrumentation serially with other builds/tests.

## Coverage and test interpretation

Coverlet 10.1.0 and ReportGenerator 5.5.11 already existed and are reused through make coverage. Default merged coverage includes SharpTestBench, SharpTestBench.Generators and SharpTestBench.Tools. Standalone Runner is not separately collected; its direct contract and native execution evidence must not be presented as measured Runner coverage. Managed coverage does not measure HDL or native machine instructions.

New cases cover publish profiles, ordered cross-host rejection pairs, unsupported modes/RIDs/output boundaries, artifact mutations and malformed metadata, apphost selection, Unix links and generated JSON integer boundaries. Many rows vary filenames or ordinary seeds for the same behavior. Report actual case counts separately from distinct capabilities; do not equate 200 iterations with 200 features. Earlier ASCII worker.unicode filenames do not establish Unicode filename coverage.

## Recommendations

1. Finish checkpoint 200 and Linux final suites, then reconcile source hashes and final committed test inventory with the exported evidence.
2. Preserve exact platform/coverage limitations in the final user report and do not re-enable GitHub CI or merge PR 62.
3. Future follow-up: collect standalone Runner managed coverage directly, extend native execution to additional actual target hosts, and add reproducible deployment metadata/signatures if distribution authenticity becomes a requirement.


---

# Independent final native worker deep review

Reviewer scope: read-only comparison of base eb9e2f6c68e51e7a807f903e02257510b7b01973 with the final working tree. No source edits, builds, or test execution were performed by this reviewer. Native execution evidence described below was supplied by the primary agent, rather than independently rerun.

## Findings

No confirmed material correctness or compatibility defects were found in the reviewed changes. No P1 or P2 findings remain from this independent review.

## NativeAOT runner

Reviewed ManifestJsonContext, EvidenceJsonContext, EvidenceRecords, JsonInput, CompilePlan, EvidenceWriter, ProcessExecution, Program and the Runner project properties.

Every Runner JsonSerializer call now passes generated type metadata. The manifest context includes RunManifest and the explicit string-array serialization used for selected test names; nested record, array and dictionary types are reachable from the declared roots. Reflection serialization is disabled in the project, which makes accidental unsupported serialization paths visible during ordinary managed execution as well as AOT publishing. Manifest property matching remains case-insensitive, unknown properties remain disallowed, and the existing strict JSON pre-validation remains in place. The explicit U+FEFF escape avoids source-encoding corruption of BOM handling.

Typed input, run, process and failure evidence preserves the former field names and values, including optional/null process details, nullable failure type, 64-bit unsigned seeds, UTC timestamps, nested input fingerprints and drain records. The evidence writer still enforces the output bound and atomic replacement; changing its boundary from object to JsonElement avoids an untyped runtime serializer. Its indented writer preserves readable JSON.

The change does not imply that managed simulation assemblies execute within NativeAOT. The orchestration executable is native, while the separately launched simulator loads the native host and managed test assemblies through hostfxr. The documentation and help text state this distinction correctly. The primary agent reports an actual Linux AOT positive simulation and classified simulator-failure evidence, which are materially stronger checks than --help alone.

## Full helper ReadyToRun deployment

Reviewed SelfInvocation and its replacement call sites in Core, benchmark, debugger, layout, lifecycle, manifest validation, fault, performance, parity, stage and workload helpers. The apphost is reused only when its exact platform filename and full containing directory agree with the implementation assembly. Managed dotnet invocation remains the fallback. Directory-based ReadyToRun retains assembly-location-dependent package and coverage behavior. The helper is intentionally self-contained ReadyToRun with JIT fallback, not NativeAOT, trimmed, or single-file.

The primary agent reports actual Linux published-helper child invocation and a nonzero PE ReadyToRun header, plus Windows published-helper startup. Those exercise important deployment assumptions. Some helper operations still require installed SDK/compiler/simulator/upstream tools; self-contained startup is not a claim that all external build dependencies disappear.

## Publish and integrity controls

Reviewed WorkerPublishCommand and WorkerArtifact. Publish plans reject unsupported or mismatched host RIDs, invalid modes, the build root itself, and output escaping the checkout build tree. Canonical checks reject linked RID/leaf output directories before invoking publish. The full-helper and runner publish profiles are explicitly different. Publishing checks executable presence and successful help before emitting and verifying metadata.

Artifact verification binds schema, worker name, RID, deployment mode and entrypoint, rejects linked artifact items, compares exact file counts and names, hashes every tracked artifact file, and requires both the helper apphost and its managed implementation. It therefore protects runtime and managed payloads rather than hashing only a launcher.

The manifest is an unsigned integrity inventory, not a trust signature or an attestation that a binary is physically AOT/ReadyToRun. It is generated by the local build and can be regenerated by someone controlling the output tree. Physical-format and functional execution checks remain separate validation evidence. File hashing/link checks are ordinary local validation and do not provide a security boundary against a concurrently malicious filesystem writer.

## Validation limits

This review statically inspected the final code and used the primary agent's reported native execution results as context. Final full tests and coverage remain the primary agent's responsibility. No claim is made that macOS, ARM64 or Windows NativeAOT was executed. The supported RID matrix is a guarded build-plan matrix. Linux x64 ReadyToRun and NativeAOT and Windows x64 ReadyToRun are the reported exercised profiles.

Default managed coverage does not separately collect the standalone Runner assembly, nor native instruction coverage or HDL coverage. The repository documents that limit. Direct Runner tests and native functional evidence complement, rather than replace, an assembly coverage report.

---

# Final independent workflow and migration deep review

Reviewed the working-tree delta from eb9e2f6c68e51e7a807f903e02257510b7b01973, including untracked final regression tests. Review was read-only; no builds or source edits were performed by this reviewer.

## Conclusion

No new blocking correctness defects found in the reviewed Make/build/test/coverage workflow or production-helper migration claims. This conclusion is source review, not an independent execution of the final gates. Final build, coverage, native execution and remote-state claims must use the root agent's recorded evidence.

## Delivered capability

- Root GNU Make frontend and tools/Makefile forwarding frontend expose build, test, coverage, publish/verify and direct apphost execution.
- Test and coverage targets explicitly build Release before executing their Release assemblies. Test runs all three managed suites and retained Python contract tests. Build uses locked restore, disabled build servers and one MSBuild node.
- The combined native target invokes a single C# publish command, avoiding the earlier parallel Make prerequisite race. Separate native-tools/native-runner targets remain available.
- Full helper deployment is directory-based self-contained ReadyToRun, with CLR/JIT fallback. Native apphost child invocations preserve the published executable rather than requiring dotnet for the helper child.
- Standalone Runner uses generated manifest/evidence JSON metadata and NativeAOT-compatible project settings.
- Publish plans reject unsupported/cross-host RIDs, output escaping build, linked output components, invalid modes. Artifact metadata covers all regular output files, validates identity and exact file/hash sets, rejects links.
- Coverlet 10.1.0 and ReportGenerator 5.5.11 are pinned already; this round exposes and qualifies their existing C# orchestration rather than newly introducing those dependencies. The coverage command validates individual reports, merged assembly sets and XML/JSON counter agreement.
- git ls-files '*.py' shows no production Python helpers under tools. Remaining Python files are independent scoreboards/integration oracles, contract test/RPC compatibility modules, benchmark reference code and Sphinx configuration. C# helpers still contain narrow upstream API adapters.

## Test quality

The new cases target deployability and trust boundaries: incompatible RID combinations, output containment, file-map integrity, malformed/duplicate JSON metadata, linked output/artifact paths, apphost name/directory selection, generated JSON round trips including full-width seeds. Positive metadata verification precedes mutation tests, making rejection checks meaningful.

Many numbered iterations are additional parameterized rows exercising a shared test body. In particular, multiple synthetic runtime filenames repeat the same tamper/remove/extra/identity behaviors. This broadens input coverage but must not be presented as 200 independent features or 200 distinct integration scenarios. This round does not add new Verilog designs or substantive HDL functional scenarios; prior pinned fixtures remain.

Unix link cases explicitly skip on Windows. State actual platform-specific skip counts from final XML. Fixed invocation paths in tests are string-only and do not create directories.

## Important limits to retain in the final report

- ReadyToRun is not NativeAOT, trimming or a single-file helper executable.
- NativeAOT orchestration does not eliminate the installed .NET runtime/hostfxr requirement for managed assemblies executed in the simulator process.
- Six RID build plans are supported; this is not evidence of successful builds on all six platforms. State only hosts actually published and executed.
- Checksums are integrity checks, not signatures or authenticity proof. Artifact verification does not independently prove an executable's code-generation format; physical binary inspection/native execution evidence supplies that qualification.
- Default merged coverage includes SharpTestBench, SharpTestBench.Generators and SharpTestBench.Tools. It does not separately collect the standalone Runner assembly, native instructions or HDL coverage. Linked Runner source tests and native smoke/functional runs are separate evidence.
- Python remains required for retained tests and selected upstream APIs. C# ownership is the accurate migration claim.
- GitHub CI was intentionally administratively disabled. Source review alone does not prove current remote workflow or PR state; verify those separately and leave PR #62 untouched.
- Concurrent invocations of mutating publish/coverage targets against the same output are not documented as supported. The ordinary make native path is serialized correctly.

## Reviewed source areas

Makefile; tools/Makefile; .gitattributes; README.md; docs/NATIVE_WORKERS.md; WorkerPublishCommand.cs; WorkerArtifact.cs; SelfInvocation.cs; Core.cs; CoverageCommand.cs; .config/dotnet-tools.json; Runner JSON/evidence changes; all new native-worker test classes, including untracked final additions.

No source changes or builds made by this reviewer.
