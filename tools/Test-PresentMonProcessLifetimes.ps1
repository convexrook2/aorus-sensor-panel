param([Parameter(Mandatory)][string]$SourceDirectory, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$SourceDirectory = (Resolve-Path -LiteralPath $SourceDirectory).Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path (Split-Path $PSScriptRoot) 'artifacts\presentmon-process-tests' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$project = Join-Path $SourceDirectory 'PresentMon\ProcessLifetimeTests.vcxproj'
[xml]$document = Get-Content -LiteralPath (Join-Path $SourceDirectory 'PresentMon\PresentMon.vcxproj') -Raw
$namespace = New-Object Xml.XmlNamespaceManager $document.NameTable
$namespace.AddNamespace('m', $document.DocumentElement.NamespaceURI)
$document.SelectSingleNode('//m:ClCompile[@Include="OutputThread.cpp"]', $namespace).ParentNode.RemoveChild(
    $document.SelectSingleNode('//m:ClCompile[@Include="OutputThread.cpp"]', $namespace)) | Out-Null
$main = $document.SelectSingleNode('//m:ClCompile[@Include="MainThread.cpp"]', $namespace)
$define = $document.CreateElement('PreprocessorDefinitions', $document.DocumentElement.NamespaceURI)
$define.InnerText = 'wmain=X890OriginalMain;%(PreprocessorDefinitions)'
$main.AppendChild($define) | Out-Null
$test = $document.CreateElement('ClCompile', $document.DocumentElement.NamespaceURI)
$test.SetAttribute('Include', (Join-Path $PSScriptRoot 'PresentMon\ProcessLifetimeTests.cpp'))
$includes = $document.CreateElement('AdditionalIncludeDirectories', $document.DocumentElement.NamespaceURI)
$includes.InnerText = (Join-Path $SourceDirectory 'PresentMon') + ';%(AdditionalIncludeDirectories)'
$test.AppendChild($includes) | Out-Null
$main.ParentNode.AppendChild($test) | Out-Null
$properties = $document.CreateElement('PropertyGroup', $document.DocumentElement.NamespaceURI)
foreach ($entry in @{ OutDir = ($OutputDirectory + '\'); IntDir = ($OutputDirectory + '\obj\'); TargetName = 'ProcessLifetimeTests' }.GetEnumerator()) {
    $element = $document.CreateElement($entry.Key, $document.DocumentElement.NamespaceURI)
    $element.InnerText = $entry.Value
    $properties.AppendChild($element) | Out-Null
}
$document.DocumentElement.InsertBefore($properties, $document.SelectSingleNode('//m:Import[contains(@Project,"Microsoft.Cpp.targets")]', $namespace)) | Out-Null
$document.Save($project)
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$visualStudio = (& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath).Trim()
$toolset = if (Test-Path -LiteralPath (Join-Path $visualStudio 'MSBuild\Microsoft\VC\v180\Platforms\x64\PlatformToolsets\v145')) { 'v145' } else { 'v143' }
& (Join-Path $visualStudio 'MSBuild\Current\Bin\MSBuild.exe') $project /t:Build /p:Configuration=Release /p:Platform=x64 "/p:PlatformToolset=$toolset" "/p:SolutionDir=$SourceDirectory\PresentMon\" /p:VcpkgEnabled=false /m:4 /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "Native regression-test build failed: $LASTEXITCODE" }
& (Join-Path $OutputDirectory 'ProcessLifetimeTests.exe')
if ($LASTEXITCODE -ne 0) { throw "Process-lifetime regression failed: $LASTEXITCODE" }
