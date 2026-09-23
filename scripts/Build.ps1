param(
    [ValidateSet('Build', 'StageMod', 'PackageMod', 'DeployMod')]
    [string]$Target = 'Build',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path (Join-Path $projectRoot 'MediumClass.local.props'))) {
    throw 'Copy MediumClass.local.props.example to MediumClass.local.props and set WrathPath first.'
}
if (-not (Get-Command msbuild -ErrorAction SilentlyContinue)) {
    throw 'Run from Developer PowerShell for Visual Studio 2022 with the .NET Framework 4.7.2 targeting pack installed.'
}
Write-Host "UNTESTED maintenance source. Requested target: $Target. Read maintenance/HANDOFF.zh-CN.md before using old saves."
Push-Location $projectRoot
try {
    & msbuild 'MediumClass.csproj' /restore "/t:$Target" "/p:Configuration=$Configuration" /nologo /v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with exit code $LASTEXITCODE. Keep the complete output for API migration review."
    }
}
finally {
    Pop-Location
}
