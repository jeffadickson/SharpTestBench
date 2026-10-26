# Shared bootstrap: this compiles the same BCL-only C# process boundary used by
# the campaign. No SDK is needed for PowerShell Add-Type, and output is bounded.
#requires -Version 7.4
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$false
if(-not ('SharpTestBench.Tools.EnduranceProcess' -as [type])) {
 Add-Type -Path (Join-Path $PSScriptRoot '../SharpTestBench.Tools/EnduranceProcess.cs')
}
function Assert-EnduranceOwnedPath {
 param([string]$Root,[string]$Path)
 if(!(Test-Path -LiteralPath $Root -PathType Container)){throw 'Owned checkout missing'}
 $Root=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Root))
 $Path=[IO.Path]::GetFullPath($Path)
 $comparison=if($IsWindows){[StringComparison]::OrdinalIgnoreCase}else{[StringComparison]::Ordinal}
 if(!$Path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,$comparison)){throw 'Owned path escapes checkout'}
 for($candidate=$Path;$candidate;$candidate=Split-Path -Parent $candidate){
  $item=Get-Item -LiteralPath $candidate -Force -ErrorAction SilentlyContinue
  if($item -and $item.LinkType){throw 'Linked campaign paths are unsupported'}
  if($candidate.Equals($Root,$comparison)){break}
 }
}
function Invoke-EnduranceNative {
 param([string]$Executable,[string[]]$Arguments,[string]$Directory,[int]$Seconds=120,[string]$Log)
 if(!(Test-Path -LiteralPath $Directory -PathType Container)){throw 'Child working directory missing'}
 $result=[SharpTestBench.Tools.EnduranceProcess]::Run($Executable,$Arguments,$Directory,[TimeSpan]::FromSeconds($Seconds),131072,[Threading.CancellationToken]::None,$null,$null)
 if($Log){
  $parent=Split-Path -Parent $Log
  if($parent -and !(Test-Path -LiteralPath $parent -PathType Container)){[IO.Directory]::CreateDirectory($parent)|Out-Null}
  [IO.File]::WriteAllBytes($Log+'.stdout.log',$result.Stdout)
  [IO.File]::WriteAllBytes($Log+'.stderr.log',$result.Stderr)
 }
 return $result
}
function ConvertTo-EnduranceLiteral {
 param([string]$Value)
 return "'"+$Value.Replace("'","''")+"'"
}
function Start-EnduranceDetached {
 param([string]$Script,[Collections.IDictionary]$Parameters,[string]$Directory,[string]$LogPrefix)
 # Encode a PowerShell expression made only of quoted literal arguments. Never
 # interpolate raw paths/options into shell text or use cmd.exe for file work.
 $entries=foreach($key in $Parameters.Keys){
  if($key -notmatch '^[A-Za-z][A-Za-z0-9]*$'){throw 'Invalid detached parameter name'}
  $value=$Parameters[$key]
  $literal=if($value -is [bool]){if($value){'$true'}else{'$false'}}else{ConvertTo-EnduranceLiteral ([string]$value)}
  (ConvertTo-EnduranceLiteral $key)+'='+$literal
 }
 $command='$enduranceDetachedParameters=@{'+($entries -join ';')+'}; & '+(ConvertTo-EnduranceLiteral $Script)+' @enduranceDetachedParameters'
 $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
 if(!(Test-Path -LiteralPath $Directory -PathType Container)){throw 'Detached working directory missing'}
 foreach($path in @(($LogPrefix+'.stdout.log'),($LogPrefix+'.stderr.log'))){if(Test-Path -LiteralPath $path){throw 'Detached logs already exist; prior evidence is preserved'}}
 $parent=Split-Path -Parent $LogPrefix
 if(!(Test-Path -LiteralPath $parent -PathType Container)){[IO.Directory]::CreateDirectory($parent)|Out-Null}
 $launchParameters=@{FilePath=(Get-Process -Id $PID).Path;ArgumentList=@('-NoProfile','-EncodedCommand',$encoded);WorkingDirectory=$Directory;RedirectStandardOutput=$LogPrefix+'.stdout.log';RedirectStandardError=$LogPrefix+'.stderr.log';PassThru=$true}
 if($IsWindows){$launchParameters.WindowStyle='Hidden'}
 if($IsLinux){
  # A new session has no controlling terminal and survives the caller's SSH
  # terminal disappearing. Do not silently claim detach if setsid is missing.
  $setsid=Get-Command setsid -CommandType Application -ErrorAction Stop
  $launchParameters.ArgumentList=@('--wait','--',('"'+$launchParameters.FilePath.Replace('"','\"')+'"'))+$launchParameters.ArgumentList
  $launchParameters.FilePath=$setsid.Source
 }
 return Start-Process @launchParameters
}
