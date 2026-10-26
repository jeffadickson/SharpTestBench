#requires -Version 7.4
[CmdletBinding()]
param([string]$Repository='https://github.com/jeffadickson/SharpTestBench.git',[Parameter(Mandatory)][string]$Workspace,[string]$SourceRef='main',
 [string]$Duration='05:00:00',[string]$Seed='20261026',[int]$PropertyCases=1000,[int]$FuzzCases=2000,[int]$MutationLimit=6,[int]$ChildSeconds=300,
 [string]$MachineLabel='remote',[string]$Python,[string]$StrictdocExecutable,[switch]$SkipCoverage,[switch]$Background)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Endurance/Common.ps1')
$Workspace=[IO.Path]::GetFullPath($Workspace)
if(Test-Path -LiteralPath $Workspace){throw 'Dedicated workspace must not exist; never reset or overwrite an existing directory'}
if($SourceRef -notmatch '^[A-Za-z0-9][A-Za-z0-9._/-]*$' -or $SourceRef.Contains('..') -or $SourceRef -match '(^|/)remote-runs/'){throw 'Use a trusted source branch/tag/SHA, never an untrusted results branch'}
if($MachineLabel -notmatch '^[a-z0-9][a-z0-9-]{0,39}$'){throw 'Invalid machine label'}
if($Repository -match '^https://'){$uri=[Uri]$Repository;if($uri.UserInfo -or $uri.Query -or $uri.Fragment){throw 'Do not embed credentials in repository URLs'}}
elseif($Repository -notmatch '^git@[a-zA-Z0-9.-]+:[a-zA-Z0-9_./-]+$' -and ![IO.Path]::IsPathFullyQualified($Repository)){throw 'Use credential-free HTTPS/SSH or an explicit local bare repository'}
$budget=[TimeSpan]::Parse($Duration,[Globalization.CultureInfo]::InvariantCulture)
if($budget.TotalSeconds -lt 1 -or $budget.TotalHours -gt 24){throw 'Duration must be1 second..24 hours'}
if($Seed -notmatch '^\d+$' -or !([UInt64]::TryParse($Seed,[ref]([UInt64]0)))){throw 'Invalid UInt64 seed'}
if($PropertyCases -lt 1 -or $PropertyCases -gt 10000 -or $FuzzCases -lt 1 -or $FuzzCases -gt 100000 -or $MutationLimit -lt 1 -or $MutationLimit -gt 6 -or $ChildSeconds -lt 1 -or $ChildSeconds -gt 1800){throw 'Campaign limits outside supported bounds'}
$parent=Split-Path -Parent $Workspace
if(!(Test-Path -LiteralPath $parent -PathType Container)){throw 'Dedicated workspace parent must already exist'}
for($directory=[IO.DirectoryInfo]::new($parent);$null -ne $directory;$directory=$directory.Parent){if($directory.LinkTarget){throw 'Linked workspace parents are unsupported'}}
if($Background){
 $parameters=@{}
 foreach($key in $PSBoundParameters.Keys){if($key -ne 'Background'){$value=$PSBoundParameters[$key];$parameters[$key]=if($value -is [Management.Automation.SwitchParameter]){[bool]$value}else{$value}}}
 $prefix=Join-Path $parent ('endurance-standalone-launch-'+[Guid]::NewGuid().ToString('N'))
 $child=Start-EnduranceDetached -Script $PSCommandPath -Parameters $parameters -Directory $parent -LogPrefix $prefix
 [pscustomobject]@{LauncherProcessId=$child.Id;Workspace=$Workspace;Logs=$prefix}|ConvertTo-Json
 exit 0
}
# Authentication must already be configured through Git's credential manager or
# SSH agent. No token is accepted as a script parameter or exported as evidence.
$env:GIT_TERMINAL_PROMPT='0';$env:GCM_INTERACTIVE='Never'
$clone=Invoke-EnduranceNative -Executable git -Arguments @('-c','protocol.ext.allow=never','clone','--no-checkout','--no-hardlinks','--config','core.autocrlf=false','--config','core.eol=lf','--',$Repository,$Workspace) -Directory $parent -Seconds 180 -Log (Join-Path $parent ('endurance-clone-'+[Guid]::NewGuid().ToString('N')))
if(!$clone.Succeeded){throw 'Clone failed; inspect authentication/network prerequisites. The newly created partial directory is preserved.'}
$resolved=Invoke-EnduranceNative -Executable git -Arguments @('rev-parse','--verify',($SourceRef+'^{commit}')) -Directory $Workspace -Seconds 15
if(!$resolved.Passed -or $resolved.Output.Trim() -notmatch '^[0-9a-f]{40}$'){throw 'Trusted source revision unavailable'}
$source=$resolved.Output.Trim()
$checked=Invoke-EnduranceNative -Executable git -Arguments @('checkout','--detach',$source) -Directory $Workspace -Seconds 30
if(!$checked.Succeeded){throw 'Fresh dedicated checkout failed; no reset or cleanup was attempted'}
if(!(Test-Path -LiteralPath (Join-Path $Workspace 'tools/Endurance.ps1'))){throw 'Selected source lacks this workflow; choose a reviewed workflow commit'}
$setup=@{Repository=$Workspace;InstallMissing=$true};if($Python){$setup.Python=$Python};if($StrictdocExecutable){$setup.StrictdocExecutable=$StrictdocExecutable}
$environment=& (Join-Path $Workspace 'tools/Setup-Endurance.ps1') @setup
$hostBuild=Invoke-EnduranceNative -Executable $environment.Dotnet -Arguments @('build','tools/SharpTestBench.Tools/SharpTestBench.Tools.csproj','-c','Release','-p:RestoreLockedMode=true','-p:NoWarn=CS1591','--disable-build-servers','-m:1','-v:quiet') -Directory $Workspace -Seconds 300 -Log (Join-Path $Workspace 'build/endurance-standalone-bootstrap')
if(!$hostBuild.Succeeded){throw 'Source host bootstrap failed; local diagnostics preserved'}
$cli=Join-Path $Workspace 'tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll'
$preflight=Invoke-EnduranceNative -Executable $environment.Dotnet -Arguments @($cli,'endurance','preflight','--expected-source',$source,'--machine-label',$MachineLabel) -Directory $Workspace -Seconds 180 -Log (Join-Path $Workspace 'build/endurance-publication-preflight')
if(!$preflight.Passed){throw 'Publication/source preflight failed before baseline and long campaign; configure authentication or correct source bytes'}
$branch=($preflight.Output|ConvertFrom-Json).branch
$baseline=Join-Path $Workspace ('build/endurance-baseline-'+[Guid]::NewGuid().ToString('N')+'.json')
$baselineRun=Invoke-EnduranceNative -Executable $environment.Dotnet -Arguments @($cli,'endurance','baseline','--output',$baseline,'--strictdoc',$environment.Strictdoc) -Directory $Workspace -Seconds 1800 -Log (Join-Path $Workspace 'build/endurance-standalone-baseline')
if(!(Test-Path -LiteralPath $baseline)){throw 'Baseline could not produce bounded source-bound evidence; publication is unsafe'}
# A failed/unavailable baseline is carried into a finished failure/unavailable
# campaign record rather than silently skipped. Valid failure evidence is sent.
$runId=[Guid]::NewGuid().ToString('N')
$arguments=@($cli,'endurance','run','--duration',$Duration,'--seed',$Seed,'--property-cases',[string]$PropertyCases,'--fuzz-cases',[string]$FuzzCases,'--mutation-limit',[string]$MutationLimit,'--child-seconds',[string]$ChildSeconds,'--machine-label',$MachineLabel,'--run-id',$runId,'--baseline',$baseline)
if($SkipCoverage){$arguments+='--skip-coverage'}
Write-Host ('Standalone run '+$runId+' at source '+$source+'; result branch '+$branch)
$campaign=Invoke-EnduranceNative -Executable $environment.Dotnet -Arguments $arguments -Directory $Workspace -Seconds ([int][Math]::Ceiling($budget.TotalSeconds+900)) -Log (Join-Path $Workspace 'build/endurance-standalone-campaign')
$report=Join-Path $Workspace ('build/endurance/'+$runId+'/report.json')
if(!(Test-Path -LiteralPath $report)){throw 'No campaign checkpoint; raw local diagnostics retained'}
$publication=Invoke-EnduranceNative -Executable $environment.Dotnet -Arguments @($cli,'endurance','publish','--run',$report,'--expected-source',$source,'--branch',$branch) -Directory $Workspace -Seconds 300 -Log (Join-Path $Workspace 'build/endurance-standalone-publication')
[Console]::Out.Write($publication.Output)
if(!$publication.Succeeded){Write-Warning 'Result upload failed. Retain the local result ref/publication.json and its exact retry command.';exit 2}
if($null -eq $campaign.ExitCode){exit 1};exit $campaign.ExitCode
