param([string]$UnityEditor = $env:ASURA_UNITY_EDITOR)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

$versionLine = Select-String -LiteralPath ProjectSettings/ProjectVersion.txt -Pattern '^m_EditorVersion: (\S+)$'
$unityVersion = $versionLine.Matches[0].Groups[1].Value
if (-not $UnityEditor) {
    $UnityEditor = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$unityVersion/Editor"
}
if (Test-Path -LiteralPath $UnityEditor -PathType Leaf) {
    $UnityEditor = Split-Path $UnityEditor -Parent
}
if (-not (Test-Path -LiteralPath (Join-Path $UnityEditor 'Unity.exe') -PathType Leaf)) {
    throw "Unity $unityVersion was not found at '$UnityEditor'. Set ASURA_UNITY_EDITOR to the Unity Editor folder or Unity.exe."
}
$unityData = (Resolve-Path -LiteralPath (Join-Path $UnityEditor 'Data')).Path
$dotnet = Join-Path $unityData 'NetCoreRuntime/dotnet.exe'
$csc = Join-Path $unityData 'DotNetSdkRoslyn/csc.dll'
$frameworkRoot = Join-Path $unityData 'NetCoreRuntime/shared/Microsoft.NETCore.App'
if (-not ((Test-Path -LiteralPath $dotnet -PathType Leaf) -and (Test-Path -LiteralPath $csc -PathType Leaf) -and (Test-Path -LiteralPath $frameworkRoot -PathType Container))) {
    throw "Unity $unityVersion at '$UnityEditor' lacks the compiler or its .NET runtime."
}
$runtimeConfig = 'Validation/loop-source-tests.runtimeconfig.json'
$runtimeVersion = (Get-Content -LiteralPath $runtimeConfig -Raw | ConvertFrom-Json).runtimeOptions.framework.version
$framework = Get-ChildItem -LiteralPath $frameworkRoot -Directory |
    Where-Object { ([version]$_.Name).Major -eq ([version]$runtimeVersion).Major -and [version]$_.Name -ge [version]$runtimeVersion } |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $framework) {
    throw "Unity $unityVersion at '$UnityEditor' lacks a .NET runtime compatible with $runtimeVersion."
}

$scratch = Join-Path ([IO.Path]::GetTempPath()) ('asura-source-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $scratch | Out-Null

function Invoke-Compiler([string]$name, [string[]]$arguments) {
    $response = Join-Path $scratch "$name.rsp"
    [IO.File]::WriteAllLines($response, $arguments)
    & $dotnet $csc "@$response"
    if ($LASTEXITCODE -ne 0) { throw "$name compilation failed (exit $LASTEXITCODE)." }
}

try {
    $coreArguments = @('/nologo', '/target:exe', "/out:`"$scratch/loop-source-tests.dll`"", '/langversion:latest')
    $coreArguments += Get-ChildItem -LiteralPath $framework.FullName -Filter '*.dll' |
        Where-Object { $_.Name -match '^(System\.|Microsoft\.|netstandard)' -and $_.Name -notmatch '\.Native\.' } |
        ForEach-Object { '/reference:"' + $_.FullName + '"' }
    $coreArguments += Get-ChildItem Assets/Marble/Core -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
    $coreArguments += @('Assets/Marble/Online/OnlineProtocol.cs', 'Assets/Marble/Online/OnlineSession.cs',
        'Assets/Marble/Online/OnlineSelfTests.cs', 'Validation/LoopSourceRunner.cs')
    Invoke-Compiler 'loop-source-tests' $coreArguments
    Copy-Item -LiteralPath $runtimeConfig -Destination $scratch
    & $dotnet "$scratch/loop-source-tests.dll"
    if ($LASTEXITCODE -ne 0) { throw "Source tests failed (exit $LASTEXITCODE)." }

    $runtimeArguments = (Get-Content -LiteralPath Validation/loop-runtime-compile.rsp).Replace('@UNITY_DATA@', $unityData).Replace('@CHECK_OUTPUT@', $scratch)
    $steamArguments = @('/nologo', '/target:library', "/out:`"$scratch/com.rlabrecque.steamworks.net.dll`"")
    $steamArguments += $runtimeArguments | Where-Object { $_ -match '^(-define:|-r:)' -and $_ -notmatch 'com\.rlabrecque\.steamworks\.net\.dll' }
    $steamArguments += Get-ChildItem Packages/com.rlabrecque.steamworks.net/Runtime -Recurse -Filter '*.cs' |
        ForEach-Object { '"' + $_.FullName + '"' }
    Invoke-Compiler 'steamworks' $steamArguments
    Invoke-Compiler 'loop-runtime-compile' $runtimeArguments

    $editorArguments = (Get-Content -LiteralPath Validation/loop-editor-compile.rsp).Replace('@UNITY_DATA@', $unityData).Replace('@CHECK_OUTPUT@', $scratch)
    Invoke-Compiler 'loop-editor-compile' $editorArguments
    Write-Output 'Source verification passed (core tests, runtime and editor compilation).'
}
finally {
    # The generated directory contains only this run's compiler output.
    Get-ChildItem -LiteralPath $scratch -File | Remove-Item
    Remove-Item -LiteralPath $scratch
}
