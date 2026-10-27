#requires -Version 7.4
<#
.SYNOPSIS
Checks static local file references in a StrictDoc HTML export.
.DESCRIPTION
StrictDoc 0.30.4 renders some source-page requirement-detail links relative to
requirements pages rather than the source page. Optional repair handles only
those two known prefixes, requires an existing target inside the export, and
validates the complete tree before writing any changes. Fragments, external URLs
and JavaScript-generated navigation are outside this file-existence check.
The pinned exporter uses double-quoted href/src start-tag attributes; this narrow
compatibility check is not a general HTML parser.
#>
param(
    [Parameter(Mandatory)][string]$Html,
    [switch]$RepairStrictDocLinks
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(!(Test-Path -LiteralPath $Html -PathType Container)){throw 'Export directory missing'}
$root=[IO.Path]::GetFullPath($Html)
$comparison=if($IsWindows){[StringComparison]::OrdinalIgnoreCase}else{[StringComparison]::Ordinal}
$prefix=$root.TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
# Reject linked components before traversal so repair cannot write outside the
# explicitly selected export, including through a linked ancestor directory.
$component=[IO.DirectoryInfo]::new($root)
while($null -ne $component){if($component.LinkTarget){throw 'Linked export path'}; $component=$component.Parent}
$queue=[Collections.Generic.Queue[string]]::new(); $queue.Enqueue($root)
$files=[Collections.Generic.List[string]]::new()
while($queue.Count){
 foreach($entry in Get-ChildItem -LiteralPath $queue.Dequeue() -Force){
  if($entry.LinkTarget){throw 'Linked export entry'}
  if($entry.PSIsContainer){$queue.Enqueue($entry.FullName)}elseif($entry.Extension -eq '.html'){$files.Add($entry.FullName)}
 }
}
if(!$files.Count){throw 'Export has no HTML pages'}
$pattern=[regex]::new('<[A-Za-z][^<>]*?\s(?:href|src)="([^"]+)"',[Text.RegularExpressions.RegexOptions]::CultureInvariant,[TimeSpan]::FromSeconds(1))
$changes=[Collections.Generic.Dictionary[string,string]]::new()
$links=0; $repairs=0
foreach($file in $files){
 if((Get-Item -LiteralPath $file).Length -gt 16MB){throw 'HTML page exceeds check budget'}
 $text=[IO.File]::ReadAllText($file)
 $builder=[Text.StringBuilder]::new($text)
 # Work backwards so replacement offsets always refer to the original text.
 $attributeMatches=$pattern.Matches($text)
 for($i=$attributeMatches.Count-1;$i -ge 0;$i--){
  $group=$attributeMatches[$i].Groups[1]
  $reference=[Net.WebUtility]::HtmlDecode($group.Value)
  if($reference -match '^(?:[a-zA-Z][a-zA-Z0-9+.-]*:|//|#)'){continue}
  $relative=($reference -split '[?#]',2)[0]
  if(!$relative){continue}
  $decoded=[Uri]::UnescapeDataString($relative)
  $target=[IO.Path]::GetFullPath($decoded,[IO.Path]::GetDirectoryName($file))
  if(!$target.Equals($root,$comparison) -and !$target.StartsWith($prefix,$comparison)){throw "Link escapes export: $reference"}
  if(!(Test-Path -LiteralPath $target)){
   $candidate=$null
   if($file.StartsWith((Join-Path $root '_source_files')+[IO.Path]::DirectorySeparatorChar,$comparison)){
    if($decoded.StartsWith('../_source_files/',[StringComparison]::Ordinal)){$candidate=$decoded.Substring(3)}
    elseif($decoded.StartsWith('requirements/',[StringComparison]::Ordinal)){$candidate=$decoded}
   }
   if(!$RepairStrictDocLinks -or !$candidate){throw "Broken export link: $file -> $reference"}
   $target=[IO.Path]::GetFullPath($candidate,$root)
   if(!$target.StartsWith($prefix,$comparison) -or !(Test-Path -LiteralPath $target -PathType Leaf)){throw 'Known link has no contained target'}
   $replacement=[IO.Path]::GetRelativePath([IO.Path]::GetDirectoryName($file),$target).Replace('\','/')+$reference.Substring($relative.Length)
   $null=$builder.Remove($group.Index,$group.Length).Insert($group.Index,[Net.WebUtility]::HtmlEncode($replacement))
   $repairs++
  }
  $links++
 }
 if($builder.ToString() -cne $text){$changes.Add($file,$builder.ToString())}
}
# Validation failure leaves every original page intact. This is not a claim of
# transactional writes across files if the OS fails during the following writes.
foreach($entry in $changes.GetEnumerator()){[IO.File]::WriteAllText($entry.Key,$entry.Value,[Text.UTF8Encoding]::new($false))}
[pscustomobject]@{htmlPages=$files.Count;localFileLinks=$links;repairedLinks=$repairs;fragmentValidation=$false;externalValidation=$false}