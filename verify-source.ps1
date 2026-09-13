$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$plugin = Join-Path $root 'src\NppXsdViewer.Plugin'

$forbidden = Get-ChildItem $root -Recurse -File -Include *.cs,*.csproj |
    Select-String -SimpleMatch 'net.r_eg.DllExport'
if ($forbidden) { throw 'Forbidden legacy net.r_eg.DllExport reference found.' }

$projectText = Get-Content (Join-Path $plugin 'NppXsdViewer.Plugin.csproj') -Raw
if ($projectText -notmatch 'UnmanagedExports\.Repack\.Upgrade') { throw 'UnmanagedExports.Repack.Upgrade build integration is missing.' }

$exportsText = Get-Content (Join-Path $plugin 'PluginInfrastructure\UnmanagedExports.cs') -Raw
foreach ($name in @('isUnicode', 'setInfo', 'getFuncsArray', 'messageProc', 'getName', 'beNotified')) {
    if ($exportsText -notmatch ('\b' + [Regex]::Escape($name) + '\s*\(')) { throw "Required Notepad++ export is missing: $name" }
}

$resolverPath = Join-Path $plugin 'PluginInfrastructure\AssemblyResolver.cs'
if (-not (Test-Path $resolverPath -PathType Leaf)) { throw 'AssemblyResolve handler is missing.' }
$resolverText = Get-Content $resolverPath -Raw
foreach ($assemblyName in @('NppXsdViewer.Schema', 'NppXsdViewer.Diagram')) {
    if ($resolverText -notmatch [Regex]::Escape($assemblyName)) { throw "AssemblyResolver does not handle: $assemblyName" }
}
if ($exportsText -notmatch 'AssemblyResolver\.Install\(\)') { throw 'AssemblyResolver is not installed from the unmanaged export layer.' }

$schemaText = Get-Content (Join-Path $root 'src\NppXsdViewer.Schema\Model\SchemaModel.cs') -Raw
foreach ($required in @('Components', 'SearchElements', 'Dependencies', 'Diagnostics', 'References', 'Patterns', 'EnumerationValues', 'Facets')) {
    if ($schemaText -notmatch [Regex]::Escape($required)) { throw "Schema model feature is missing: $required" }
}

$diagramText = Get-Content (Join-Path $root 'src\NppXsdViewer.Diagram\SchemaDiagramControl.cs') -Raw
foreach ($required in @('expandedCompositors', 'CollapseAll', 'ExpandAll', 'SelectionChanged', 'Copy schema path', 'SetRootType', 'TypeDefinitionRequested')) {
    if ($diagramText -notmatch [Regex]::Escape($required)) { throw "Diagram feature is missing: $required" }
}
foreach ($required in @('BadgeKind.Optional', 'DashStyle.Dot', '"optional"', '"[1]"', 'RevealPath')) {
    if ($diagramText -notmatch [Regex]::Escape($required)) { throw "Required/optional diagram feature is missing: $required" }
}

foreach ($forbiddenText in @('MaxDepth', 'FitToWindow', 'TextRenderer.DrawText', 'DrawMiniMap', 'MiniMapWidth', 'MiniMapHeight', 'MiniMapMargin')) {
    if ($diagramText -match [Regex]::Escape($forbiddenText)) { throw "Legacy diagram feature remains: $forbiddenText" }
}

$formText = Get-Content (Join-Path $plugin 'UI\XsdViewerForm.cs') -Raw
foreach ($required in @('Components', 'SearchElements', 'Search schema', 'Complex Types', 'Simple Types', 'Used by', 'Dependencies', 'Problems', 'Expand all', 'Collapse all', 'propertyStack', 'PropertySection', 'ShowSearchElement')) {
    if ($formText -notmatch [Regex]::Escape($required)) { throw "Viewer UI feature is missing: $required" }
}
foreach ($forbiddenText in @('Enumerációk', 'Korlátozások', 'Attribútumok', 'Mind kinyit', 'Mind becsuk', 'Mélység:', 'Illesztés', 'propertyTabs', 'ConfigurePropertyTabs')) {
    if ($formText -match [Regex]::Escape($forbiddenText)) { throw "Non-English or legacy UI label remains: $forbiddenText" }
}

Write-Host 'Source verification: OK'
