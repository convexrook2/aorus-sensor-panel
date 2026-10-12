param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$frameViewSdk = 'C:\Program Files\NVIDIA Corporation\FrameViewSDK\SDK\Public_Release'
$vcEnvironment = 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\VC\Auxiliary\Build\vcvars64.bat'
$nativeOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $nativeOutput -Force | Out-Null
$sourcePath = Join-Path $projectRoot 'SensorCollector\Native\FrameViewBridge.cpp'
$dllPath = Join-Path $nativeOutput 'NvidiaFrameViewBridge.dll'
$objectPath = Join-Path $nativeOutput 'NvidiaFrameViewBridge.obj'
$pdbPath = Join-Path $nativeOutput 'NvidiaFrameViewBridge.pdb'
$importPath = Join-Path $nativeOutput 'NvidiaFrameViewBridge.lib'
$commandPath = Join-Path $nativeOutput 'BuildFrameViewBridge.cmd'
# Fixed local build paths are quoted as cmd arguments, not evaluated by PowerShell.
$commands = @(
    '@echo off',
    ('call "{0}" >nul' -f $vcEnvironment),
    'if errorlevel 1 exit /b %errorlevel%',
    ('cl.exe /nologo /std:c++17 /EHsc /O2 /MD /LD /I"{0}" "{1}" "{0}\FvSDK_x64.lib" /Fo"{2}" /Fe"{3}" /Fd"{4}" /link /IMPLIB:"{5}" /PDB:"{4}"' -f $frameViewSdk,$sourcePath,$objectPath,$dllPath,$pdbPath,$importPath),
    'exit /b %errorlevel%'
)
[IO.File]::WriteAllLines($commandPath, $commands, [Text.Encoding]::ASCII)
& $commandPath
if ($LASTEXITCODE -ne 0) { throw "FrameView native bridge build failed: $LASTEXITCODE" }
