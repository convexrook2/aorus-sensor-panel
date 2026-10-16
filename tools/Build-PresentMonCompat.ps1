param([string]$SourceDirectory, [string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot
if(-not $SourceDirectory){$SourceDirectory=Join-Path $repoRoot ('artifacts/presentmon-build-'+(Get-Date -Format yyyyMMdd-HHmmss))}
if(-not $OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot 'PresentMon'}
if(Test-Path -LiteralPath $SourceDirectory){throw 'Use a new source directory; existing work is never overwritten.'}
$sourceRevision='e13fce6acdb55a808fd8318175a56863e532d95f'
function Invoke-Git([string[]]$Arguments) {
    & git @Arguments
    if($LASTEXITCODE -ne 0){throw "Git failed: $($Arguments -join ' ')"}
}
Start-Transcript -Path ($SourceDirectory+'.console.txt')
try {
Invoke-Git @('clone','--depth','1','--branch','v2.6.0','--single-branch','https://github.com/GameTechDev/PresentMon.git',$SourceDirectory)
$head=(& git -C $SourceDirectory rev-parse HEAD).Trim()
if($head -ne $sourceRevision){throw "Unexpected upstream revision: $head"}
Invoke-Git @('clone','--depth','1','--branch','v1.3.2','https://github.com/USCiLab/cereal.git',(Join-Path $SourceDirectory 'deps/cereal'))
Invoke-Git @('clone','--depth','1','--branch','v1.0.4','https://github.com/cameron314/concurrentqueue.git',(Join-Path $SourceDirectory 'deps/concurrentqueue/moodycamel'))
if((& git -C (Join-Path $SourceDirectory 'deps/cereal') rev-parse HEAD).Trim() -ne 'ebef1e929807629befafbb2918ea1a08c7194554'){throw 'Unexpected cereal revision.'}
if((& git -C (Join-Path $SourceDirectory 'deps/concurrentqueue/moodycamel') rev-parse HEAD).Trim() -ne '6dd38b8a1dbaa7863aa907045f32308a56a6ff5d'){throw 'Unexpected concurrentqueue revision.'}
$patch=Join-Path $PSScriptRoot 'PresentMon/x890-compat.patch'
Invoke-Git @('-C',$SourceDirectory,'apply','--check',$patch)
Invoke-Git @('-C',$SourceDirectory,'apply',$patch)
# The console does not use the GUI, CLI11, or asynchronous named-pipe components.
@'
<Project>
  <ItemDefinitionGroup>
    <PreBuildEvent><Command></Command></PreBuildEvent>
    <ClCompile>
      <AdditionalIncludeDirectories>$(MSBuildThisFileDirectory)deps\cereal\include;$(MSBuildThisFileDirectory)deps;%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
      <DisableSpecificWarnings>5054;%(DisableSpecificWarnings)</DisableSpecificWarnings>
    </ClCompile>
  </ItemDefinitionGroup>
  <ItemGroup Condition="'$(ProjectName)'=='CommonUtilities'">
    <ClCompile Remove="cli\CliFramework.cpp;pipe\*.cpp;log\NamedPipeMarshallReceiver.cpp;log\NamedPipeMarshallSender.cpp" />
  </ItemGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $SourceDirectory 'Directory.Build.targets') -Encoding utf8
New-Item -ItemType Directory -Force -Path (Join-Path $SourceDirectory 'build/obj/generated') | Out-Null
Set-Content -LiteralPath (Join-Path $SourceDirectory 'build/obj/generated/version.h') -Value 'char const* PRESENT_MON_VERSION = "2.6.0-x890-compat";' -Encoding ascii
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs=(& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath).Trim()
if(-not $vs){throw 'Visual Studio C++ tools are required.'}
$toolsets=Join-Path $vs 'MSBuild/Microsoft/VC'
$toolset=if(Test-Path (Join-Path $toolsets 'v180/Platforms/x64/PlatformToolsets/v145')){'v145'}else{'v143'}
Push-Location -LiteralPath $SourceDirectory
try {
    & (Join-Path $vs 'MSBuild/Current/Bin/MSBuild.exe') 'PresentMon/ConsoleApplication.sln' /t:Build /p:Configuration=Release /p:Platform=x64 "/p:PlatformToolset=$toolset" /p:VcpkgEnabled=false /m:4 /nologo /verbosity:minimal /fl '/flp:logfile=build.log;verbosity=normal'
    if($LASTEXITCODE -ne 0){throw "PresentMon build failed: $LASTEXITCODE"}
}finally{Pop-Location}
& (Join-Path $PSScriptRoot 'Test-PresentMonProcessLifetimes.ps1') -SourceDirectory $SourceDirectory -OutputDirectory (Join-Path $SourceDirectory 'process-lifetime-tests')
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$binary=Join-Path $OutputDirectory 'PresentMon-2.6.0-x890-compat-x64.exe'
Copy-Item -LiteralPath (Join-Path $SourceDirectory 'PresentMon/build/Release/PresentMon-2.6.0-x64.exe') -Destination $binary
@('PresentMon 2.6.0 X890 compatibility build',"Upstream: https://github.com/GameTechDev/PresentMon/commit/$sourceRevision",'Patch and reproducible build: tools/PresentMon/x890-compat.patch and tools/Build-PresentMonCompat.ps1',
    (Get-Content -Raw (Join-Path $SourceDirectory 'LICENSE.txt')),'cereal v1.3.2: https://github.com/USCiLab/cereal/tree/v1.3.2',
    (Get-Content -Raw (Join-Path $SourceDirectory 'deps/cereal/LICENSE')),'concurrentqueue v1.0.4: https://github.com/cameron314/concurrentqueue/tree/v1.0.4',
    (Get-Content -Raw (Join-Path $SourceDirectory 'deps/concurrentqueue/moodycamel/LICENSE.md')),
    'NVIDIA portions of PresentMon: Copyright (c) 2025 NVIDIA CORPORATION & AFFILIATES. All rights reserved. MIT license (full permission and warranty text above).',
    'RapidJSON bundled with cereal: Copyright (C) 2015 THL A29 Limited, a Tencent company, and Milo Yip. All rights reserved. MIT license (full permission and warranty text above).') |
    Set-Content -LiteralPath (Join-Path $OutputDirectory 'COMPAT-NOTICES.txt') -Encoding utf8
[pscustomobject]@{Revision=$head;PatchSha256=(Get-FileHash -LiteralPath $patch).Hash;BinarySha256=(Get-FileHash -LiteralPath $binary).Hash;Toolset=$toolset;Source=$SourceDirectory} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $SourceDirectory 'build-receipt.json') -Encoding utf8
Write-Host "Built $binary"
}finally{Stop-Transcript}
