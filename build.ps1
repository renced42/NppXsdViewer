$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'verify-source.ps1')

$solution = Join-Path $PSScriptRoot 'NppXsdViewer.sln'
msbuild $solution /restore /m /p:Configuration=Release /p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild sikertelen. Exit code: $LASTEXITCODE"
}

$distRoot = Join-Path $PSScriptRoot 'dist'
$pluginDir = Join-Path $distRoot 'NppXsdViewer'
$pluginZip = Join-Path $distRoot 'NppXsdViewer-plugin.zip'

$requiredFiles = @(
    'NppXsdViewer.dll',
    'NppXsdViewer.Schema.dll',
    'NppXsdViewer.Diagram.dll'
)

foreach ($fileName in $requiredFiles) {
    $filePath = Join-Path $pluginDir $fileName
    if (-not (Test-Path $filePath -PathType Leaf)) {
        throw "A telepitesi csomag hianyos. Hianyzo fajl: $filePath"
    }
}

if (Test-Path $pluginZip) {
    Remove-Item $pluginZip -Force
}

Compress-Archive -Path $pluginDir -DestinationPath $pluginZip -CompressionLevel Optimal

Write-Host ''
Write-Host 'Build sikeres.' -ForegroundColor Green
Write-Host 'Notepad++-ba masolando konyvtar:' -ForegroundColor Cyan
Write-Host "  $pluginDir"
Write-Host 'Telepitheto ZIP:' -ForegroundColor Cyan
Write-Host "  $pluginZip"
Write-Host ''
Write-Host 'A Notepad++ celkonyvtara:'
Write-Host '  C:\Program Files\Notepad++\plugins\NppXsdViewer\'
