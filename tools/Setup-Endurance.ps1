#requires -Version 7.4
[CmdletBinding()]
param([string]$Repository=(Split-Path -Parent $PSScriptRoot),[string]$Python,[string]$StrictdocExecutable,[switch]$InstallMissing)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Endurance/Common.ps1')
$Repository=[IO.Path]::GetFullPath($Repository)
if(!(Test-Path -LiteralPath $Repository -PathType Container)){throw 'Setup checkout directory missing'}
if(!(Test-Path -LiteralPath (Join-Path $Repository 'global.json'))){throw 'Setup requires a SharpTestBench source checkout'}
$pin=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Endurance/dependencies.json') -Raw|ConvertFrom-Json
$sdkSelection=Get-Content -LiteralPath (Join-Path $Repository 'global.json') -Raw|ConvertFrom-Json
if($sdkSelection.sdk.version -ne $pin.sdkVersion){throw 'global.json and endurance dependency pins differ'}
if([Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString() -ne 'X64'){throw 'Automated setup currently supports x64; other architectures need separately qualified prerequisites'}
if(!$IsWindows -and !$IsLinux){throw 'Automated setup supports Windows and Linux'}
if($IsLinux){
 $release=@{}
 foreach($line in Get-Content -LiteralPath /etc/os-release){if($line -match '^([A-Z_]+)=(.*)$'){$release[$matches[1]]=$matches[2].Trim('"')}}
 if(!(($release.ID -eq 'ubuntu' -and $release.VERSION_ID -eq '24.04') -or ($release.ID -eq 'debian' -and $release.VERSION_ID -eq '12'))){throw 'Automated Linux setup is limited to Ubuntu24.04 and Debian12'}
}
$store=Join-Path $Repository 'build/endurance-tools'
function Assert-UnlinkedDirectory([string]$Path){
 for($directory=[IO.DirectoryInfo]::new([IO.Path]::GetFullPath($Path));$null -ne $directory;$directory=$directory.Parent){if($directory.Exists -and $directory.LinkTarget){throw 'Linked setup directories are unsupported'}}
}
Assert-UnlinkedDirectory $store
if(!(Test-Path -LiteralPath $store -PathType Container)){[IO.Directory]::CreateDirectory($store)|Out-Null}
function Get-PinnedSdk([string]$Candidate){
 if(!$Candidate -or !(Test-Path -LiteralPath (Join-Path $Candidate ('sdk/'+$pin.sdkVersion+'/dotnet.dll')))){return $null}
 $exe=Join-Path $Candidate $(if($IsWindows){'dotnet.exe'}else{'dotnet'})
 if(!(Test-Path -LiteralPath $exe)){return $null}
 $checked=Invoke-EnduranceNative -Executable $exe -Arguments @('--version') -Directory $Repository -Seconds 15
 if($checked.Passed -and $checked.Output.Trim() -eq $pin.sdkVersion){return $exe}
 return $null
}
# Reuse existing matching tools first, including a per-user SDK after a system
# updater has replaced a patch version. Never change global.json to fit PATH.
$candidates=@($env:CS_DOTNET_ROOT,$env:DOTNET_ROOT,(Join-Path $HOME '.dotnet'))
$candidates+=@(Get-Command dotnet -All -ErrorAction SilentlyContinue|ForEach-Object {
 # Linux package managers commonly expose dotnet through /usr/bin symlinks.
 $executable=Get-Item -LiteralPath $_.Source
 if($executable.LinkTarget){$executable=$executable.ResolveLinkTarget($true)}
 Split-Path -Parent $executable.FullName
})
$candidates+=Join-Path $store ('sdk-'+$pin.sdkVersion)
$sdk=$null
foreach($candidate in $candidates|Where-Object {$_}|Select-Object -Unique){$sdk=Get-PinnedSdk $candidate;if($sdk){break}}
if(!$sdk){
 if(!$InstallMissing){throw 'Pinned SDK unavailable; provision it or rerun setup with -InstallMissing for isolated installation'}
 $rid=if($IsWindows){'win-x64'}else{'linux-x64'}
 $download=@($pin.downloads|Where-Object rid -eq $rid)[0]
 $uri=[Uri]$download.url
 if($uri.Scheme -ne 'https' -or $uri.Host -notin @('builds.dotnet.microsoft.com','download.visualstudio.microsoft.com','dotnetcli.azureedge.net','dotnetcli.blob.core.windows.net') -or $download.sha512 -notmatch '^[0-9a-f]{128}$'){throw 'Invalid official SDK download pin'}
 $destination=Join-Path $store ('sdk-'+$pin.sdkVersion)
 if(Test-Path -LiteralPath $destination){throw 'Existing isolated SDK directory is incomplete or unrelated; preserve and inspect it rather than overwrite'}
 $archive=Join-Path $store ('sdk-download-'+[Guid]::NewGuid().ToString('N'))
 $http=[Net.Http.HttpClient]::new();$timeout=[Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(180))
 try{
  $response=$http.GetAsync($uri,[Net.Http.HttpCompletionOption]::ResponseHeadersRead,$timeout.Token).GetAwaiter().GetResult()
  $response.EnsureSuccessStatusCode()|Out-Null
  $downloadStream=$response.Content.ReadAsStreamAsync($timeout.Token).GetAwaiter().GetResult()
  $output=[IO.File]::Open($archive,[IO.FileMode]::CreateNew)
  try{
   $buffer=[byte[]]::new(65536);$total=0L
   while(($count=$downloadStream.ReadAsync($buffer,0,$buffer.Length,$timeout.Token).GetAwaiter().GetResult()) -gt 0){$total+=$count;if($total -gt 536870912){throw 'SDK archive exceeds512MiB'};$output.Write($buffer,0,$count)}
  }finally{$output.Dispose();$downloadStream.Dispose();$response.Dispose()}
 }finally{$timeout.Dispose();$http.Dispose()}
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash.ToLowerInvariant() -ne $download.sha512){throw 'SDK SHA512 mismatch; nothing was extracted or executed'}
 $staging=Join-Path $store ('sdk-staging-'+[Guid]::NewGuid().ToString('N'))
 if(Test-Path -LiteralPath $staging){throw 'SDK staging directory already exists; preserve it'}
 [IO.Directory]::CreateDirectory($staging)|Out-Null
 if($IsWindows){[IO.Compression.ZipFile]::ExtractToDirectory($archive,$staging,$false)}else{
  $file=[IO.File]::OpenRead($archive);$gzip=[IO.Compression.GZipStream]::new($file,[IO.Compression.CompressionMode]::Decompress)
  try{[System.Formats.Tar.TarFile]::ExtractToDirectory($gzip,$staging,$false)}finally{$gzip.Dispose();$file.Dispose()}
 }
 [IO.Directory]::Move($staging,$destination)
 $sdk=Get-PinnedSdk $destination
 if(!$sdk){throw 'Verified SDK payload could not execute; check documented OS prerequisites'}
}
$env:DOTNET_ROOT=Split-Path -Parent $sdk;$env:CS_DOTNET_ROOT=$env:DOTNET_ROOT;$env:Path=$env:DOTNET_ROOT+[IO.Path]::PathSeparator+$env:Path
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_NOLOGO='1';$env:STB_ENDURANCE_POWERSHELL_VERSION=$PSVersionTable.PSVersion.ToString()
if(!(Get-Command git -ErrorAction SilentlyContinue)){throw 'Git is an explicit bootstrap prerequisite'}
$pyCandidates=@($Python,$env:CS_PYTHON,(Join-Path $Repository $(if($IsWindows){'.venv-windows/Scripts/python.exe'}else{'.venv/bin/python'})))
$pyCandidates+=@(Get-Command python3.12,python,python3 -ErrorAction SilentlyContinue|Where-Object {$_.Source -notlike '*WindowsApps*'}|ForEach-Object Source)
$pythonExe=$null
foreach($candidate in $pyCandidates|Where-Object {$_}|Select-Object -Unique){
 if(!(Test-Path -LiteralPath $candidate)){continue}
 $version=Invoke-EnduranceNative -Executable $candidate -Arguments @('--version') -Directory $Repository -Seconds 15
 if($version.Passed -and $version.Output.Trim() -match '^Python 3\.12\.\d+$'){$pythonExe=$candidate;$env:STB_ENDURANCE_PYTHON_VERSION=$version.Output.Trim();break}
}
if(!$pythonExe){throw 'CPython3.12 is an explicit prerequisite; install it from official sources before standalone setup'}
$env:CS_PYTHON=$pythonExe
$strictCandidates=@($StrictdocExecutable,$env:CS_STRICTDOC,(Join-Path $Repository $(if($IsWindows){'build/strictdoc-env/Scripts/strictdoc.exe'}else{'build/strictdoc-env/bin/strictdoc'})))
$strictCandidates+=Join-Path $store $(if($IsWindows){'strictdoc-0.30.4/Scripts/strictdoc.exe'}else{'strictdoc-0.30.4/bin/strictdoc'})
$strictCandidates+=@(Get-Command strictdoc -ErrorAction SilentlyContinue|ForEach-Object Source)
$strict=$null
foreach($candidate in $strictCandidates|Where-Object {$_}|Select-Object -Unique){
 if(!(Test-Path -LiteralPath $candidate)){continue}
 $version=Invoke-EnduranceNative -Executable $candidate -Arguments @('--version') -Directory $Repository -Seconds 15
 if($version.Passed -and $version.Output -match '(?<![0-9.])0\.30\.4(?![0-9.])'){$strict=$candidate;break}
}
if(!$strict){
 if(!$InstallMissing){throw 'StrictDoc0.30.4 unavailable; provide -StrictdocExecutable or use -InstallMissing for a hash-locked isolated venv'}
 $venv=Join-Path $store 'strictdoc-0.30.4'
 if(Test-Path -LiteralPath $venv){throw 'Existing isolated StrictDoc directory is incomplete or unrelated; do not overwrite it'}
 $created=Invoke-EnduranceNative -Executable $pythonExe -Arguments @('-m','venv',$venv) -Directory $Repository -Seconds 60 -Log (Join-Path $store 'venv-create')
 if(!$created.Succeeded){throw 'StrictDoc venv creation failed'}
 $venvPython=Join-Path $venv $(if($IsWindows){'Scripts/python.exe'}else{'bin/python'})
 $installed=Invoke-EnduranceNative -Executable $venvPython -Arguments @('-m','pip','--isolated','install','--index-url','https://pypi.org/simple','--require-hashes','--only-binary=:all:','--disable-pip-version-check','-r',(Join-Path $PSScriptRoot 'Endurance/strictdoc-requirements.txt')) -Directory $Repository -Seconds 600 -Log (Join-Path $store 'strictdoc-install')
 if(!$installed.Succeeded){throw 'Hash-locked StrictDoc installation failed; retain logs and inspect prerequisites'}
 $strict=Join-Path $venv $(if($IsWindows){'Scripts/strictdoc.exe'}else{'bin/strictdoc'})
}
$env:CS_STRICTDOC=$strict
$strictVersion=Invoke-EnduranceNative -Executable $strict -Arguments @('--version') -Directory $Repository -Seconds 15
if(!$strictVersion.Passed -or $strictVersion.Output -notmatch '(?<![0-9.])0\.30\.4(?![0-9.])'){throw 'Selected StrictDoc could not execute with the pinned version'}
$env:STB_ENDURANCE_STRICTDOC_VERSION='0.30.4'
$restored=Invoke-EnduranceNative -Executable $sdk -Arguments @('tool','restore') -Directory $Repository -Seconds 180 -Log (Join-Path $store 'dotnet-tools')
if(!$restored.Succeeded){throw 'Pinned local .NET tool restore failed'}
[pscustomobject]@{Dotnet=$sdk;Python=$pythonExe;Strictdoc=$strict;PowerShell=$PSVersionTable.PSVersion.ToString();SdkVersion=$pin.sdkVersion;InstallPolicy='reuse matching tools; isolate missing SDK/StrictDoc; no automatic administrator or sudo operations'}
