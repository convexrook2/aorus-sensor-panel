param([Parameter(Mandatory=$true)][string]$PackageRoot)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$licenses = Join-Path $PackageRoot 'Licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
$deps = Get-Content -LiteralPath (Join-Path $PackageRoot 'SensorPanel.deps.json') -Raw | ConvertFrom-Json
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('AORUS SENSOR PANEL - FAN MADE / THIRD-PARTY NOTICES')
$lines.Add('Independent project. GIGABYTE/AORUS trademarks and supplied artwork belong to their owners. No endorsement is claimed.')
$lines.Add('The unmodified LibreHardwareMonitor 0.9.6 library is MPL-2.0; corresponding source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6')
$lines.Add('PresentMon 2.6.0: MIT; source https://github.com/GameTechDev/PresentMon/tree/v2.6.0 ; full license in PresentMon-LICENSE.txt.')
$lines.Add('.NET Desktop Runtime: installed separately and shared; it is not bundled in this package. Microsoft source: https://github.com/dotnet/runtime and https://github.com/dotnet/wpf.')
foreach ($library in $deps.libraries.PSObject.Properties | Where-Object {$_.Value.type -eq 'package'}) {
    $parts = $library.Name.Split('/'); $id = $parts[0]; $version = $parts[1]
    $directory = Join-Path $env:USERPROFILE ('.nuget\packages\' + $id.ToLowerInvariant() + '\' + $version)
    $nuspecPath = Join-Path $directory ($id.ToLowerInvariant()+'.nuspec')
    if (-not (Test-Path -LiteralPath $nuspecPath)) { throw ('Missing package notices metadata: ' + $library.Name) }
    [xml]$spec = Get-Content -LiteralPath $nuspecPath -Raw
    $metadata = $spec.package.metadata
    $lines.Add('')
    $lines.Add($id + ' ' + $version)
    $lines.Add('Package: https://www.nuget.org/packages/' + $id + '/' + $version)
    if ($metadata.license) { $lines.Add('License: ' + $metadata.license.InnerText + ' (' + $metadata.license.type + ')') }
    if ($metadata.licenseUrl) { $lines.Add('License URL: ' + $metadata.licenseUrl) }
    if ($metadata.repository.url) { $lines.Add('Source: ' + $metadata.repository.url) }
    $archivePath = Join-Path $directory ($id.ToLowerInvariant()+'.'+$version+'.nupkg')
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        foreach ($entry in $archive.Entries | Where-Object { $_.Name -match '^(LICENSE|COPYING|NOTICE|ThirdPartyNotices)(\.|$)' -or ($metadata.license.type -eq 'file' -and $_.FullName -eq $metadata.license.InnerText) }) {
            $destination = Join-Path $licenses ($id + '-' + $version + '-' + $entry.Name)
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$destination,$true)
        }
    } finally { $archive.Dispose() }
}
$config = Get-Content -LiteralPath (Join-Path $PackageRoot 'SensorPanel.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $config.runtimeOptions.includedFrameworks) {
    $id = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $directory = Join-Path $env:USERPROFILE ('.nuget\packages\' + $id + '\' + $framework.version)
    $texts = @(Get-ChildItem -LiteralPath $directory -File | Where-Object Name -match '^(LICENSE|THIRD-PARTY-NOTICES)(\.|$)')
    if ($texts.Count -eq 0) { throw ('Missing bundled runtime license: ' + $id) }
    foreach ($text in $texts) { Copy-Item -LiteralPath $text.FullName -Destination (Join-Path $licenses ($framework.name + '-' + $framework.version + '-' + $text.Name)) }
}
$mpl = Join-Path $licenses 'MPL-2.0.txt'
if (-not (Test-Path -LiteralPath $mpl)) {
    $client = [Net.WebClient]::new()
    try { $client.DownloadFile('https://raw.githubusercontent.com/LibreHardwareMonitor/LibreHardwareMonitor/v0.9.6/LICENSE',$mpl) }
    finally { $client.Dispose() }
}
$lines | Set-Content -LiteralPath (Join-Path $PackageRoot 'THIRD-PARTY-NOTICES.txt') -Encoding UTF8
Write-Host 'Package notices and supplied component license texts copied.'
