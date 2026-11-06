# Genera el instalador de Casca Print Agent en .\artifacts
# Uso: .\build-installer.ps1 [-Version 1.0.0]
param(
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$publishDir = Join-Path $root 'artifacts\publish'

Write-Host "==> Publicando agente $Version (win-x64, autocontenido)..."
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
dotnet publish (Join-Path $root 'src\Casca.PrintAgent\Casca.PrintAgent.csproj') `
    -c Release -r win-x64 --self-contained `
    -p:Version=$Version -p:DebugType=none `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Fallo dotnet publish.' }

$candidates = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

if (-not $candidates) {
    Write-Warning 'No se encontro Inno Setup 6. Instalalo con: winget install JRSoftware.InnoSetup'
    Write-Host "El agente publicado quedo en: $publishDir"
    exit 1
}

Write-Host '==> Compilando instalador...'
& ($candidates | Select-Object -First 1) "/DPublishDir=$publishDir" "/DAppVersion=$Version" (Join-Path $root 'installer\CascaPrintAgent.iss')
if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion del instalador.' }

Write-Host "==> Listo: $(Join-Path $root "artifacts\CascaPrintAgent-Setup-$Version.exe")"
