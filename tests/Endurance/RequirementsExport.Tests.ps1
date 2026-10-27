# Fixtures model the generated source-page paths, not arbitrary HTML parsing.
Describe 'StrictDoc exported file links' {
 BeforeEach {
  $root=Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
  if(!(Test-Path -LiteralPath $TestDrive -PathType Container)){throw 'TestDrive absent'}
  if(Test-Path -LiteralPath $root){throw 'Owned fixture already exists'}
  $null=New-Item -ItemType Directory -Path $root
  $directory=Join-Path $root 'requirements'
  if(!(Test-Path -LiteralPath $directory -PathType Container)){$null=New-Item -ItemType Directory -Path $directory}
  $directory=Join-Path $root '_source_files/src'
  if(!(Test-Path -LiteralPath $directory -PathType Container)){$null=New-Item -ItemType Directory -Path $directory}
  [IO.File]::WriteAllText((Join-Path $root 'requirements/selection.html'),'<html></html>')
  $page=Join-Path $root '_source_files/src/example.cs.html'
  $checker=Join-Path $PSScriptRoot '../../tools/Check-RequirementsExport.ps1'
 }
 It 'repairs both known source-page prefixes and passes a read-only second check' {
  [IO.File]::WriteAllText($page,'<a href="requirements/selection.html#R">R</a><a href="../_source_files/src/example.cs.html#L">S</a>')
  $result=& $checker -Html $root -RepairStrictDocLinks
  $result.repairedLinks | Should Be 2
  [IO.File]::ReadAllText($page) | Should Match '../../requirements/selection.html#R'
  (& $checker -Html $root).repairedLinks | Should Be 0
 }
 It 'preserves already valid references and external links byte for byte' {
  $text='<a href="../../requirements/selection.html#R">R</a><a href="https://example.com/a">E</a>'
  [IO.File]::WriteAllText($page,$text)
  $result=& $checker -Html $root -RepairStrictDocLinks
  $result.repairedLinks | Should Be 0
  [IO.File]::ReadAllText($page) | Should Be $text
 }
 It 'rejects missing unknown links before writing earlier planned repairs' {
  $text='<a href="missing.html">M</a><a href="requirements/selection.html">R</a>'
  [IO.File]::WriteAllText($page,$text)
  {& $checker -Html $root -RepairStrictDocLinks} | Should Throw 'export'
  [IO.File]::ReadAllText($page) | Should Be $text
 }
 It 'requires explicit repair and an existing known destination' {
  [IO.File]::WriteAllText($page,'<a href="requirements/selection.html">R</a>')
  {& $checker -Html $root} | Should Throw 'export'
  [IO.File]::WriteAllText($page,'<a href="requirements/absent.html">R</a>')
  {& $checker -Html $root -RepairStrictDocLinks} | Should Throw 'contained target'
 }
 It 'rejects traversal outside the selected export' {
  [IO.File]::WriteAllText($page,'<a href="../../../outside.html">X</a>')
  {& $checker -Html $root -RepairStrictDocLinks} | Should Throw 'export'
 }
 It 'rejects empty exports' {
  $empty=Join-Path $root 'empty'
  if(!(Test-Path -LiteralPath $empty -PathType Container)){$null=New-Item -ItemType Directory -Path $empty}
  {& $checker -Html $empty} | Should Throw 'Export'
 }
 It 'rejects percent-encoded traversal' {
  [IO.File]::WriteAllText($page,'<a href="%2e%2e/%2e%2e/%2e%2e/outside.html">X</a>')
  {& $checker -Html $root -RepairStrictDocLinks} | Should Throw 'escapes export'
 }
 It 'rejects a missing export and an oversized page' {
  {& $checker -Html (Join-Path $root 'absent')} | Should Throw 'directory missing'
  [IO.File]::WriteAllText($page,('x' * (16MB+1)))
  {& $checker -Html $root} | Should Throw 'budget'
 }
 It 'rejects a linked child directory' {
  $kind=if($IsWindows){'Junction'}else{'SymbolicLink'}
  $null=New-Item -ItemType $kind -Path (Join-Path $root 'linked') -Target (Join-Path $root 'requirements')
  {& $checker -Html $root -RepairStrictDocLinks} | Should Throw 'Linked export entry'
 }
 It 'rejects a linked export path' {
  $kind=if($IsWindows){'Junction'}else{'SymbolicLink'}
  $link=Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
  $null=New-Item -ItemType $kind -Path $link -Target $root
  {& $checker -Html $link -RepairStrictDocLinks} | Should Throw 'Linked export path'
 }
 It 'does not interpret displayed source text as HTML attributes' {
  $text='<span>&lt;a href="missing.html"&gt;source&lt;/a&gt;</span>'
  [IO.File]::WriteAllText($page,$text)
  $result=& $checker -Html $root -RepairStrictDocLinks
  $result.localFileLinks | Should Be 0
  [IO.File]::ReadAllText($page) | Should Be $text
 }
}
