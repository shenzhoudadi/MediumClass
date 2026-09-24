param([string]$GamePath)
$ErrorActionPreference = 'Stop'

function Test-GameFolder([string]$Path) {
    return (Test-Path -LiteralPath (Join-Path $Path 'Wrath_Data\Managed\Assembly-CSharp.dll') -PathType Leaf)
}

function Find-ModFolder([string]$Root, [string]$Id) {
    $foundFolders = @()
    foreach ($folder in @(Get-ChildItem -LiteralPath (Join-Path $Root 'Mods') -Directory)) {
        $infoPath = Join-Path $folder.FullName 'Info.json'
        if (-not (Test-Path -LiteralPath $infoPath -PathType Leaf)) { continue }
        try { $info = Get-Content -LiteralPath $infoPath -Raw | ConvertFrom-Json }
        catch { continue }
        if ($info.Id -eq $Id) { $foundFolders += $folder.FullName }
    }
    if ($foundFolders.Count -ne 1) {
        throw "Expected one installed $Id mod, found $($foundFolders.Count). Check the Mods folder."
    }
    return $foundFolders[0]
}

$staging = $null
$zipPath = $null
try {
    if (-not $GamePath) {
        $candidates = @()
        foreach ($drive in @(Get-PSDrive -PSProvider FileSystem)) {
            foreach ($suffix in @('SteamLibrary\steamapps\common\Pathfinder Second Adventure',
                'Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure')) {
                $candidate = Join-Path $drive.Root $suffix
                if (Test-GameFolder $candidate) { $candidates += $candidate }
            }
        }
        $candidates = @($candidates | Select-Object -Unique)
        if ($candidates.Count -eq 1) { $GamePath = $candidates[0] }
        else {
            Add-Type -AssemblyName System.Windows.Forms
            $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
            $dialog.Description = '选择游戏主目录（里面能看到 Wrath_Data 文件夹）'
            $dialog.ShowNewFolderButton = $false
            try {
                if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
                    throw 'Cancelled. No files were collected.'
                }
                $GamePath = $dialog.SelectedPath
            } finally { $dialog.Dispose() }
        }
    }
    $GamePath = (Resolve-Path -LiteralPath $GamePath).Path
    if (-not (Test-GameFolder $GamePath)) { throw 'Select the game root folder containing Wrath_Data.' }
    Write-Host "Game folder: $GamePath"
    Write-Host 'Read-only collection: game DLLs and two dependency manifests. No saves or logs.'
    $managed = Join-Path $GamePath 'Wrath_Data\Managed'
    $modMenu = Find-ModFolder $GamePath 'ModMenu'
    $ttt = Find-ModFolder $GamePath 'TabletopTweaks-Core'
    $umm = $null
    foreach ($path in @((Join-Path $managed 'UnityModManager'), (Join-Path $GamePath 'UnityModManager'), $managed)) {
        if (Test-Path -LiteralPath (Join-Path $path 'UnityModManager.dll') -PathType Leaf) {
            $umm = $path
            break
        }
    }
    if (-not $umm) { throw 'UnityModManager.dll was not found in the usual game installation folders.' }
    $required = @(
        (Join-Path $umm '0Harmony.dll'),
        (Join-Path $modMenu 'ModMenu.dll'),
        (Join-Path $ttt 'TabletopTweaks-Core.dll')
    )
    foreach ($path in $required) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required file missing: $path" }
    }
    $entries = @()
    # Include top-level managed DLLs to supply transitive compile references as well.
    $groups = @(
        @{ Source = $managed; Destination = 'Wrath_Data/Managed' },
        @{ Source = $umm; Destination = 'Wrath_Data/Managed/UnityModManager' },
        @{ Source = $modMenu; Destination = 'Mods/ModMenu' },
        @{ Source = $ttt; Destination = 'Mods/TabletopTweaks-Core' }
    )
    foreach ($group in $groups) {
        $files = @(Get-ChildItem -LiteralPath $group.Source -File -Filter '*.dll')
        if ($group.Source -eq $umm -and $umm -eq $managed -and $group.Destination -like '*/UnityModManager') {
            $files = @($files | Where-Object { $_.Name -in @('UnityModManager.dll', '0Harmony.dll') })
        }
        foreach ($file in $files) {
            $entries += @{ Source = $file.FullName; Relative = "$($group.Destination)/$($file.Name)" }
        }
    }
    foreach ($group in @(@{Source=$modMenu; Destination='Mods/ModMenu'}, @{Source=$ttt; Destination='Mods/TabletopTweaks-Core'})) {
        $entries += @{ Source=(Join-Path $group.Source 'Info.json'); Relative="$($group.Destination)/Info.json" }
    }
    $staging = Join-Path ([IO.Path]::GetTempPath()) ('MediumClass-refs-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $staging | Out-Null
    $records = @()
    foreach ($entry in $entries) {
        $dest = Join-Path $staging $entry.Relative
        New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $entry.Source -Destination $dest
        $file = Get-Item -LiteralPath $dest
        $records += [ordered]@{
            path = $entry.Relative
            bytes = $file.Length
            sha256 = (Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash.ToLowerInvariant()
            file_version = $file.VersionInfo.FileVersion
        }
    }
    [ordered]@{
        purpose = 'Private build references for MediumClass; not a mod installer'
        collected_utc = [DateTime]::UtcNow.ToString('o')
        files = $records
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $staging 'reference-manifest.json') -Encoding UTF8
    $name = 'MediumClass-build-references-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8) + '.zip'
    $zipPath = Join-Path $PSScriptRoot $name
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($staging, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
    Write-Host ''
    Write-Host '完成。把下面这个 ZIP 发给维护者，不要导入 UMM：' -ForegroundColor Green
    Write-Host $zipPath
    Write-Host ('Size: {0:N1} MB; files: {1}' -f ((Get-Item -LiteralPath $zipPath).Length / 1048576), $records.Count)
    Write-Host '里面是构建参考文件。工具没有修改游戏，没有上传文件。'
} catch {
    if ($zipPath -and (Test-Path -LiteralPath $zipPath)) { Remove-Item -LiteralPath $zipPath -Force }
    Write-Host ('FAILED: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host '请把此窗口报错截图发给维护者。'
    exit 1
} finally {
    if ($staging -and (Test-Path -LiteralPath $staging)) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
