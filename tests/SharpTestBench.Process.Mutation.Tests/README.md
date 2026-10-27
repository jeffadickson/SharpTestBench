# Process synchronization-context mutation check

The private xUnit2/VSTest driver uses the same pinned packages as the endurance
guard driver. The normal xUnit3 Tools suite source-links the identical two cases;
its build symbol supplies xUnit3 cancellation. The isolated subject source-links
only the exact production EnduranceProcess.cs. This keeps the established six-
guard campaign and its evidence schema unchanged.

Select SDK10.0.402, restore the solution in locked mode and build Release first.
From the checkout, use PowerShell7.4+ to select only the read/write context flags:

```powershell
$repo=(Get-Location).Path
$parent=Join-Path $repo 'build'
if(!(Test-Path -LiteralPath $parent -PathType Container)){throw 'Build first'}
$out=Join-Path $parent ('process-mutation-'+[guid]::NewGuid().ToString('N'))
if(Test-Path -LiteralPath $out){throw 'Output already exists'}
New-Item -ItemType Directory -Path $out | Out-Null
$source=[IO.File]::ReadAllText((Join-Path $repo 'tools/SharpTestBench.Tools/EnduranceProcess.cs'))
$spans=@()
foreach($anchor in @('await stream.WriteAsync','await stream.ReadAsync')){
    $at=$source.IndexOf($anchor,[StringComparison]::Ordinal)
    if($at -lt 0){throw 'Source anchor missing'}
    $start=$source.IndexOf('false',$at,[StringComparison]::Ordinal)
    if($start -lt 0){throw 'Context flag missing'}
    $spans+='**/EnduranceProcess.cs{'+$start+'..'+($start+5)+'}'
}
@{'stryker-config'=@{
    project='SharpTestBench.Process.MutationSubject.csproj'
    'test-projects'=@('SharpTestBench.Process.Mutation.Tests.csproj')
    'test-runner'='vstest'; configuration='Release'; concurrency=1
    mutate=$spans; reporters=@('json'); verbosity='info'
    'additional-timeout'=1000; 'timeout-ratio'=1.5
    thresholds=@{high=100;low=0;break=0}
}} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'config.json')
$tests=Join-Path $repo 'tests/SharpTestBench.Process.Mutation.Tests'
if(!(Test-Path -LiteralPath $tests -PathType Container)){throw 'Driver missing'}
Push-Location -LiteralPath $tests
try {
    dotnet tool run dotnet-stryker --config-file (Join-Path $out 'config.json') --output (Join-Path $out 'output')
    if($LASTEXITCODE){throw 'Mutation execution failed'}
} finally { Pop-Location }
```

Require both selected boolean mutants to be killed, with no selected survivors,
timeouts, uncovered, invalid or pending results. The CLI threshold is not this
acceptance check. Record the report hash, source hash, two control cases and
excluded outcomes separately. FlushAsync context capture is also disabled in
production, but its pipe flush can complete synchronously; this narrow campaign
makes no mutation-sensitivity claim for that flag, other process code or the
whole product. The recording context allows progress while counting posts, so a
mutant fails an assertion instead of merely timing out a blocked UI loop.