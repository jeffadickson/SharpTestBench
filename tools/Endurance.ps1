#requires -Version 7.4
[CmdletBinding()]
param(
 [ValidateSet('Run','Replay','Baseline','Preflight','Publish','Import','Review','Recover','Stop','Help')][string]$Mode='Run',
 [string]$Duration='05:00:00',[string]$Seed='20261026',[int]$PropertyCases=1000,[int]$FuzzCases=2000,[int]$MutationLimit=6,
 [int]$ChildSeconds=300,[int]$LogBytes=131072,[int]$ArtifactBytes=536870912,[int]$MaxBatches=10000,[string]$MachineLabel='local',
 [string]$RunId,[string]$Run,[string]$Baseline,[string]$ExpectedSource,[string]$Branch,[string]$Summary,[string]$Checksums,[string]$Commit,
 [string]$Python,[string]$StrictdocExecutable,[switch]$InstallMissing,[switch]$SkipCoverage,[switch]$NoPush,[switch]$Background
)
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$false
$repository=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Endurance/Common.ps1')
if($Mode -eq 'Help'){
 Write-Output 'Endurance: -Mode Run -Duration hh:mm:ss (default05:00:00), Replay, Baseline, Preflight, Publish, Import, Review, Recover, Stop. Add -Background for an explicitly requested detached run. See docs/ENDURANCE.md.'
 exit 0
}
if($Mode -eq 'Run'){
 $budget=[TimeSpan]::Parse($Duration,[Globalization.CultureInfo]::InvariantCulture)
 if($budget.TotalSeconds -lt 1 -or $budget.TotalHours -gt 24){throw 'Duration must be1 second..24 hours'}
 if($Seed -notmatch '^\d+$' -or !( [UInt64]::TryParse($Seed,[ref]([UInt64]0)))){throw 'Invalid UInt64 seed'}
 if(!$RunId){$RunId=[Guid]::NewGuid().ToString('N')}
 if($RunId -notmatch '^[0-9a-f]{32}$'){throw 'Invalid run ID'}
 if($Background){
  $arguments=@{}
  foreach($key in $PSBoundParameters.Keys){if($key -ne 'Background'){$value=$PSBoundParameters[$key];$arguments[$key]=if($value -is [Management.Automation.SwitchParameter]){[bool]$value}else{$value}}}
  $arguments.RunId=$RunId
  $prefix=Join-Path $repository ('build/endurance-launchers/'+$RunId+'-'+[Guid]::NewGuid().ToString('N'))
  $child=Start-EnduranceDetached -Script $PSCommandPath -Parameters $arguments -Directory $repository -LogPrefix $prefix
  [pscustomobject]@{RunId=$RunId;LauncherProcessId=$child.Id;Report=(Join-Path $repository ('build/endurance/'+$RunId+'/report.json'));Logs=$prefix}|ConvertTo-Json
  exit 0
 }
}
if($Mode -eq 'Stop'){
 if($Run -notmatch '^[0-9a-f]{32}$'){throw 'Stop requires -Run with a local run ID'}
 $directory=Join-Path $repository ('build/endurance/'+$Run)
 Assert-EnduranceOwnedPath -Root $repository -Path (Join-Path $directory 'stop.request')
 if(!(Test-Path -LiteralPath (Join-Path $directory 'local-owner.json'))){throw 'Local campaign has not started'}
 [IO.File]::WriteAllText((Join-Path $directory 'stop.request'),'stop requested')
 exit 0
}
$setup=@{Repository=$repository;InstallMissing=$InstallMissing}
if($Python){$setup.Python=$Python};if($StrictdocExecutable){$setup.StrictdocExecutable=$StrictdocExecutable}
$environment=& (Join-Path $PSScriptRoot 'Setup-Endurance.ps1') @setup
$bootstrap=Join-Path $repository ('build/endurance-bootstrap/'+[Guid]::NewGuid().ToString('N'))
$built=Invoke-EnduranceNative -Executable $environment.Dotnet -Arguments @('build','tools/SharpTestBench.Tools/SharpTestBench.Tools.csproj','-c','Release','-p:RestoreLockedMode=true','-p:NoWarn=CS1591','--disable-build-servers','-m:1','-v:quiet') -Directory $repository -Seconds 300 -Log $bootstrap
if(!$built.Succeeded){throw ('Endurance host bootstrap failed; see '+$bootstrap+' logs')}
$cli=Join-Path $repository 'tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll'
$arguments=[Collections.Generic.List[string]]::new();$arguments.Add($cli);$arguments.Add('endurance');$arguments.Add($Mode.ToLowerInvariant())
function Add-Option([string]$Name,[string]$Value){if($Value){$arguments.Add($Name);$arguments.Add($Value)}}
switch($Mode){
 'Run'{
  Add-Option '--duration' $Duration;Add-Option '--seed' $Seed;Add-Option '--property-cases' $PropertyCases;Add-Option '--fuzz-cases' $FuzzCases;Add-Option '--mutation-limit' $MutationLimit
  Add-Option '--child-seconds' $ChildSeconds;Add-Option '--log-bytes' $LogBytes;Add-Option '--artifact-bytes' $ArtifactBytes;Add-Option '--max-batches' $MaxBatches;Add-Option '--machine-label' $MachineLabel;Add-Option '--run-id' $RunId;Add-Option '--baseline' $Baseline
  if($SkipCoverage){$arguments.Add('--skip-coverage')}
  Write-Host ('Run '+$RunId+'; checkpoint '+(Join-Path $repository ('build/endurance/'+$RunId+'/report.json')))
 }
 'Replay'{Add-Option '--seed' $Seed;Add-Option '--property-cases' $PropertyCases;Add-Option '--fuzz-cases' $FuzzCases}
 'Baseline'{Add-Option '--output' $Baseline;Add-Option '--strictdoc' $environment.Strictdoc}
 'Preflight'{Add-Option '--machine-label' $MachineLabel;Add-Option '--branch' $Branch}
 'Publish'{Add-Option '--run' $Run;Add-Option '--branch' $Branch;if($NoPush){$arguments.Add('--no-push')}}
 'Import'{Add-Option '--summary' $Summary;Add-Option '--checksums' $Checksums}
 'Review'{Add-Option '--commit' $Commit}
 'Recover'{Add-Option '--run' $Run}
}
if($Mode -in @('Preflight','Publish','Import','Review')){Add-Option '--expected-source' $ExpectedSource}
$seconds=if($Mode -eq 'Run'){[int][Math]::Ceiling($budget.TotalSeconds+900)}elseif($Mode -eq 'Baseline'){1800}else{300}
# The persisted report is capped at512KiB; allow a bounded envelope for console
# serialization/newline differences rather than truncating valid report JSON.
$captureBytes=if($Mode -eq 'Review'){1048576}else{131072}
$result=Invoke-EnduranceNative -Executable $environment.Dotnet -Arguments $arguments.ToArray() -Directory $repository -Seconds $seconds -Log ($bootstrap+'-host') -MaximumBytes $captureBytes
if($Mode -eq 'Review' -and $result.Truncated){throw 'Structured review response exceeded its bound; partial JSON is not a successful review'}
[Console]::Out.Write($result.Output);[Console]::Error.Write([Text.Encoding]::UTF8.GetString($result.Stderr))
if($result.TimedOut -or !$result.Settled){Write-Error 'Outer host deadline/cleanup failure; retain evidence and use Recover for a dead running checkpoint';exit 1}
if($null -eq $result.ExitCode){exit 1};exit $result.ExitCode
