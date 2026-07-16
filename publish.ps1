<#
.SYNOPSIS
    Bumps the patch version, then publishes Dvojnik as a single self-contained .exe.

.DESCRIPTION
    Every publish bumps the version: 1.0.1 -> 1.0.2 -> 1.0.3 ...
    The version lives in Version.props, which the csproj imports.

    The output is one .exe with the .NET runtime bundled in, so target machines
    need nothing installed.

.PARAMETER Runtime
    Target runtime identifier. Defaults to win-x64; pass win-arm64 for ARM machines.

.PARAMETER NoBump
    Republish the current version without incrementing it.

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -Runtime win-arm64
#>
[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64',
    [switch]$NoBump
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$versionProps = Join-Path $root 'Version.props'
$outDir = Join-Path $root 'publish'

# --- Read current version ---
$xml = [xml](Get-Content $versionProps -Raw)
$node = $xml.Project.PropertyGroup.VersionPrefix
if (-not $node) { throw "VersionPrefix not found in $versionProps" }

$parts = $node.ToString().Trim().Split('.')
if ($parts.Count -ne 3) { throw "Expected a three-part version, got '$node'" }

[int]$major = $parts[0]; [int]$minor = $parts[1]; [int]$patch = $parts[2]

# --- Bump patch ---
if (-not $NoBump) {
    $patch++
    $new = "$major.$minor.$patch"
    $xml.Project.PropertyGroup.VersionPrefix = $new
    $xml.Save($versionProps)
    Write-Host "Version bumped: $node -> $new" -ForegroundColor Cyan
} else {
    $new = "$major.$minor.$patch"
    Write-Host "Publishing existing version $new (no bump)" -ForegroundColor Yellow
}

# --- Publish ---
Write-Host "Publishing $Runtime self-contained single file..." -ForegroundColor Cyan

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

& dotnet publish (Join-Path $root 'FileExplorerClone') `
    -c Release `
    -r $Runtime `
    -o $outDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$exe = Join-Path $outDir 'Dvojnik.exe'
$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)

Write-Host ""
Write-Host "Published Dvojnik $new" -ForegroundColor Green
Write-Host "  $exe ($sizeMb MB, no .NET install needed)" -ForegroundColor Green
