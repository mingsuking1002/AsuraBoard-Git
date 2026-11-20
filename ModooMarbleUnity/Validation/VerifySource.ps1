$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$taskRuntime = 'C:\Unity Editer\6000.3.9f1\Editor\Data\NetCoreRuntime'
$taskCsc = 'C:\Unity Editer\6000.3.9f1\Editor\Data\DotNetSdkRoslyn\csc.dll'
$taskCompileArgs = @('/nologo','/target:exe','/out:Validation/loop-source-tests.dll','/langversion:latest')
$taskCompileArgs += Get-ChildItem "$taskRuntime/shared/Microsoft.NETCore.App/6.0.21" -Filter '*.dll' | Where-Object { $_.Name -match '^(System\.|Microsoft\.|netstandard)' -and $_.Name -notmatch '\.Native\.' } | ForEach-Object { '/reference:' + $_.FullName }
$taskCompileArgs += Get-ChildItem Assets/Marble/Core -Filter '*.cs' | ForEach-Object FullName
$taskCompileArgs += @('Assets/Marble/Online/OnlineProtocol.cs','Assets/Marble/Online/OnlineSession.cs','Assets/Marble/Online/OnlineSelfTests.cs','Validation/LoopSourceRunner.cs')
& "$taskRuntime/dotnet.exe" $taskCsc @taskCompileArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& "$taskRuntime/dotnet.exe" Validation/loop-source-tests.dll
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& "$taskRuntime/dotnet.exe" $taskCsc '@Validation/loop-runtime-compile.rsp'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& "$taskRuntime/dotnet.exe" $taskCsc '@Validation/loop-editor-compile.rsp'
exit $LASTEXITCODE
