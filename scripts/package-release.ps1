param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$publishRoot = Join-Path $artifactsRoot 'publish-win-x64'
$archivePath = Join-Path $artifactsRoot "PhotoSearchPdf-v$Version-win-x64.zip"
$checksumsPath = Join-Path $artifactsRoot "PhotoSearchPdf-v$Version-SHA256.txt"

if (Test-Path -LiteralPath $publishRoot) {
    $resolvedPublish = (Resolve-Path -LiteralPath $publishRoot).Path
    if (-not $resolvedPublish.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected path: $resolvedPublish"
    }
    Remove-Item -LiteralPath $resolvedPublish -Recurse -Force
}

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
dotnet test (Join-Path $repoRoot 'PhotoSearchPdf.slnx') --configuration Release
dotnet publish (Join-Path $repoRoot 'src\PhotoSearchPdf.App\PhotoSearchPdf.App.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=false `
    --output $publishRoot

if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $archivePath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumsPath -Value "$hash  $(Split-Path $archivePath -Leaf)" -Encoding Ascii

Write-Host "Created: $archivePath"
Write-Host "SHA256:  $hash"
