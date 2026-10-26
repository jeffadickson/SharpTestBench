#requires -Version 7.4
# Bootstrap tests deliberately avoid SDK installation, GitHub and real campaigns.
$repository=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if(!(Test-Path -LiteralPath $repository -PathType Container)){throw 'Test checkout missing'}
. (Join-Path $repository 'tools/Endurance/Common.ps1')
$owned=Join-Path ([IO.Path]::GetTempPath()) ('stb-endurance-pester-'+[Guid]::NewGuid().ToString('N'))
if(Test-Path -LiteralPath $owned){throw 'Owned test directory unexpectedly exists'}
New-Item -ItemType Directory -Path $owned|Out-Null
try {
 Describe 'Endurance bootstrap boundaries' {
  It 'Quotes literal PowerShell values without evaluation' {
   $value='spaces Ω ''quotes'' $(throw "must not execute") `ticks`'
   $literal=ConvertTo-EnduranceLiteral $value
   (& ([ScriptBlock]::Create($literal))) | Should Be $value
  }
  It 'Rejects an existing standalone workspace before cloning' {
   $result=Invoke-EnduranceNative -Executable (Get-Process -Id $PID).Path -Arguments @('-NoProfile','-File',(Join-Path $repository 'tools/Remote-Endurance.ps1'),'-Workspace',$owned) -Directory $repository -Seconds 20
   $result.ExitCode | Should Be 1
   [Text.Encoding]::UTF8.GetString($result.Stderr) | Should Match 'must not exist'
  }
  It 'Rejects invalid campaign duration before dependency setup' {
   $result=Invoke-EnduranceNative -Executable (Get-Process -Id $PID).Path -Arguments @('-NoProfile','-File',(Join-Path $repository 'tools/Endurance.ps1'),'-Duration','00:00:00') -Directory $repository -Seconds 20
   $result.ExitCode | Should Be 1
   [Text.Encoding]::UTF8.GetString($result.Stderr) | Should Match 'Duration must be'
  }
  It 'Rejects overflowing seeds before dependency setup' {
   $result=Invoke-EnduranceNative -Executable (Get-Process -Id $PID).Path -Arguments @('-NoProfile','-File',(Join-Path $repository 'tools/Endurance.ps1'),'-Seed','18446744073709551616') -Directory $repository -Seconds 20
   $result.ExitCode | Should Be 1
   [Text.Encoding]::UTF8.GetString($result.Stderr) | Should Match 'Invalid UInt64 seed'
  }
  It 'Rejects a results branch as executable source' {
   $result=Invoke-EnduranceNative -Executable (Get-Process -Id $PID).Path -Arguments @('-NoProfile','-File',(Join-Path $repository 'tools/Remote-Endurance.ps1'),'-Workspace',(Join-Path $owned 'absent'),'-SourceRef','remote-runs/unsafe') -Directory $repository -Seconds 20
   $result.ExitCode | Should Be 1
   [Text.Encoding]::UTF8.GetString($result.Stderr) | Should Match 'trusted source'
  }
  It 'Rejects an invalid standalone seed before cloning' {
   $result=Invoke-EnduranceNative -Executable (Get-Process -Id $PID).Path -Arguments @('-NoProfile','-File',(Join-Path $repository 'tools/Remote-Endurance.ps1'),'-Workspace',(Join-Path $owned 'absent-seed'),'-Seed','-1') -Directory $repository -Seconds 20
   $result.ExitCode | Should Be 1
   [Text.Encoding]::UTF8.GetString($result.Stderr) | Should Match 'Invalid UInt64 seed'
  }
  It 'Preserves either existing detached log before starting another process' {
   foreach($stream in @('stdout','stderr')){
    $prefix=Join-Path $owned ('existing-'+$stream);$path=$prefix+'.'+$stream+'.log'
    [IO.File]::WriteAllText($path,'retain previous diagnostic')
    {Start-EnduranceDetached -Script (Join-Path $owned 'not-executed.ps1') -Parameters @{} -Directory $owned -LogPrefix $prefix} | Should Throw 'Detached logs already exist'
    [IO.File]::ReadAllText($path) | Should Be 'retain previous diagnostic'
   }
  }
  It 'Rejects linked stop paths without changing the target' {
   $target=Join-Path $owned 'stop-target'
   if(!(Test-Path -LiteralPath $target -PathType Container)){New-Item -ItemType Directory -Path $target|Out-Null}
   Set-Content -LiteralPath (Join-Path $target 'local-owner.json') -Value '{}'
   $marker=Join-Path $target 'stop.request';[IO.File]::WriteAllText($marker,'preserve target')
   $parent=Join-Path $repository 'build/endurance'
   if(!(Test-Path -LiteralPath $parent -PathType Container)){New-Item -ItemType Directory -Path $parent|Out-Null}
   $runId=[Guid]::NewGuid().ToString('N');$link=Join-Path $parent $runId
   $kind=if($IsWindows){'Junction'}else{'SymbolicLink'}
   New-Item -ItemType $kind -Path $link -Target $target|Out-Null
   try {
    $result=Invoke-EnduranceNative -Executable (Get-Process -Id $PID).Path -Arguments @('-NoProfile','-File',(Join-Path $repository 'tools/Endurance.ps1'),'-Mode','Stop','-Run',$runId) -Directory $repository -Seconds 20
    $result.ExitCode | Should Be 1
    [Text.Encoding]::UTF8.GetString($result.Stderr) | Should Match 'Linked campaign'
    [IO.File]::ReadAllText($marker) | Should Be 'preserve target'
   } finally { Remove-Item -LiteralPath $link -Force }
  }
  It 'Passes detached literal paths and switches without shell interpolation' {
   $script=Join-Path $owned 'child script.ps1';$output=Join-Path $owned 'output Ω.json'
   Set-Content -LiteralPath $script -Value 'param([string]$Output,[string]$Value,[switch]$Flag); [IO.File]::WriteAllText($Output,(@{Value=$Value;Flag=[bool]$Flag}|ConvertTo-Json))'
   $value='literal Ω ''quote'' $(no-execution); &'
   $child=Start-EnduranceDetached -Script $script -Parameters @{Output=$output;Value=$value;Flag=$true} -Directory $owned -LogPrefix (Join-Path $owned 'detached')
   try {
    $child.WaitForExit(20000) | Should Be $true
    $child.ExitCode | Should Be 0
    $actual=Get-Content -LiteralPath $output -Raw|ConvertFrom-Json
    $actual.Value | Should Be $value
    $actual.Flag | Should Be $true
   } finally { if(!$child.HasExited){$child.Kill($true);$child.WaitForExit(5000)|Out-Null};$child.Dispose() }
  }
 }
} finally {
 # Remove only the unique directory owned by this test invocation.
 $resolved=[IO.Path]::GetFullPath($owned)
 if((Split-Path -Parent $resolved) -ne [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath())){throw 'Invalid owned test path'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
