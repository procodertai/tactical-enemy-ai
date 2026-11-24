param(
    [string]$UnityExe
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$versionLine = Get-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -TotalCount 1
if ($versionLine -notmatch '^m_EditorVersion: (.+)$') { throw 'Unity project version is missing.' }
$version = $Matches[1]
if (-not $UnityExe) {
    $candidates = @(
        "D:\Unity\Hub\Editor\$version\Editor\Unity.exe",
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe")
    )
    $UnityExe = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (-not $UnityExe -or -not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) {
    throw "Install Unity $version with Windows Build Support or pass -UnityExe with the editor path."
}
$UnityExe = (Resolve-Path -LiteralPath $UnityExe).Path
$windowsSupport = Join-Path (Split-Path -Parent $UnityExe) 'Data/PlaybackEngines/WindowsStandaloneSupport'
if (-not (Test-Path -LiteralPath $windowsSupport -PathType Container)) {
    throw "Windows Build Support is missing for $UnityExe."
}

$buildRoot = (New-Item -ItemType Directory -Path (Join-Path $project 'Builds') -Force).FullName
$logs = (New-Item -ItemType Directory -Path (Join-Path $project 'Logs') -Force).FullName
$release = Join-Path $buildRoot 'LastLight'
$stamp = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$staging = Join-Path $buildRoot ".lastlight-$stamp"
$backup = Join-Path $buildRoot "LastLight.previous-$stamp"
$log = Join-Path $logs "windows-build-$stamp.log"
$exe = Join-Path $staging 'LastLight.exe'
foreach ($directory in $buildRoot, $release) {
    if ((Test-Path -LiteralPath $directory) -and
        ((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Build path must be an ordinary directory: $directory"
    }
}

Write-Host "Building Last Light with Unity $version. Log: $log"
$unityArgs = '-batchmode -nographics -quit -buildTarget Win64 -projectPath "' + $project +
    '" -executeMethod TacticalGame.Editor.ProjectChecks.BuildWindows -lastLightBuildOutput "' + $exe +
    '" -logFile "' + $log + '"'
$process = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -WorkingDirectory $project -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) {
    if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 28 }
    throw "Unity exited with code $($process.ExitCode). See $log. Close any editor already using this project."
}
foreach ($file in 'LastLight.exe', 'UnityPlayer.dll', 'LastLight_Data/level0', 'LastLight_Data/level1') {
    if (-not (Test-Path -LiteralPath (Join-Path $staging $file) -PathType Leaf)) {
        throw "The staged build is incomplete: missing $file. See $log."
    }
}

$savedPrevious = $false
if (Test-Path -LiteralPath $release) {
    Move-Item -LiteralPath $release -Destination $backup
    $savedPrevious = $true
}
try {
    Move-Item -LiteralPath $staging -Destination $release
} catch {
    if ($savedPrevious -and -not (Test-Path -LiteralPath $release)) {
        Move-Item -LiteralPath $backup -Destination $release
    }
    throw
}
if ($savedPrevious) { Write-Host "Previous build: $backup" }
Write-Host "Windows build ready: $(Join-Path $release 'LastLight.exe')"
