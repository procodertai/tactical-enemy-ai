param(
    [string]$EditorData = 'D:\Unity\Hub\Editor\6000.6.0f1\Editor\Data'
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskOutput = Join-Path $taskRoot 'Artifacts\CompileCheck'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskSdk = & dotnet --list-sdks | Select-Object -Last 1
if ($taskSdk -notmatch '^([^ ]+) \[(.+)\]$') { throw 'No installed .NET SDK found.' }
$taskCompiler = Join-Path $Matches[2] ($Matches[1] + '\Roslyn\bincore\csc.dll')
$taskReferences = @()
$taskReferences += Get-ChildItem -LiteralPath (Join-Path $EditorData 'NetStandard\ref\2.1.0') -Filter '*.dll' -ErrorAction SilentlyContinue
if ($taskReferences.Count -eq 0) {
    $taskReferences += Get-ChildItem -LiteralPath (Join-Path $EditorData 'MonoBleedingEdge\lib\mono\4.8-api') -Filter '*.dll'
    $taskReferences += Get-ChildItem -LiteralPath (Join-Path $EditorData 'MonoBleedingEdge\lib\mono\4.8-api\Facades') -Filter '*.dll'
}
$taskReferences += Get-ChildItem -LiteralPath (Join-Path $EditorData 'Managed\UnityEngine') -Filter '*.dll'
$taskReferences += Get-ChildItem -LiteralPath (Join-Path $EditorData 'Managed') -Filter 'UnityEditor.*Module.dll'
$taskArguments = @('/nologo', '/target:library', '/nostdlib+', '/langversion:9', '/define:UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_6000_6_OR_NEWER;UNITY_6000_0_OR_NEWER;UNITY_2023_1_OR_NEWER;UNITY_2022_2_OR_NEWER', ('/out:"' + (Join-Path $taskOutput 'TacticalGame.CompileCheck.dll') + '"'))
$taskArguments += $taskReferences | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$taskArguments += Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Assets\TacticalGame') -Filter '*.cs' -Recurse | ForEach-Object { '"' + $_.FullName + '"' }
$taskResponse = Join-Path $taskOutput 'compile.rsp'
Set-Content -LiteralPath $taskResponse -Value $taskArguments -Encoding utf8
& dotnet $taskCompiler ('@' + $taskResponse)
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output 'PASS: all game and editor C# scripts compile against the selected Unity assemblies.'
