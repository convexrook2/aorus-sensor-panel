$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$manifest = Get-Content -LiteralPath '.\artifacts\installer\package-manifest.json' -Raw | ConvertFrom-Json
if ($manifest.Version -ne '1.0.2' -or $manifest.SelfContained -ne $false) { throw 'Build v1.0.2 shared-runtime package first.' }
$userKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{1B7C308F-694A-4C92-8C95-218009316F78}_is1'
if (Test-Path -LiteralPath $userKey) { throw 'Preserve existing per-user installation; isolated test cannot run.' }
$production = Join-Path $env:ProgramFiles 'AORUS Sensor Panel FAN MADE'
$productionHash = (Get-FileHash -LiteralPath (Join-Path $production 'SensorPanel.exe')).Hash
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$scheduler = New-Object -ComObject Schedule.Service; $scheduler.Connect()
$taskName = 'AORUS-FAN-MADE-1B7C308F-' + $sid
$beforeTask = $scheduler.GetFolder('\').GetTask($taskName).Xml
$target = [IO.Path]::GetFullPath((Join-Path $projectRoot ('artifacts\shared-install-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))))
if (-not $target.StartsWith($projectRoot+'\artifacts\shared-install-', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe isolated test path.' }
New-Item -ItemType Directory -Path $target | Out-Null
foreach ($legacy in @('coreclr.dll','hostfxr.dll')) { Copy-Item -LiteralPath (Join-Path $production $legacy) -Destination (Join-Path $target $legacy) }
Write-Host 'Isolated per-user install: no shortcuts/startup, no live panel, production installation preserved.'
$arguments = '/CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /SP- /NORESTART /NOICONS /TASKS="" /DIR="'+$target+'" /LOG="'+(Join-Path $target 'install-test.log')+'"'
$install = Start-Process $manifest.Installer -ArgumentList $arguments -Wait -PassThru
if ($install.ExitCode -ne 0) { throw ('Isolated setup failed: '+$install.ExitCode) }
foreach ($file in $manifest.Files) {
    $path = Join-Path $target $file.Path
    if ((Get-FileHash -LiteralPath $path).Hash -ne $file.SHA256) { throw ('Installed digest mismatch: '+$file.Path) }
}
foreach ($legacy in @('coreclr.dll','hostfxr.dll')) { if (Test-Path -LiteralPath (Join-Path $target $legacy)) { throw 'Old bundled runtime was not removed.' } }
$check = Start-Process (Join-Path $target 'SensorPanel.exe') -ArgumentList '--check-mapping' -Wait -PassThru
if ($check.ExitCode -ne 0) { throw 'Shared-runtime installed mapping/resource check failed.' }
$installedBytes = (Get-ChildItem -LiteralPath $target -File -Recurse | Measure-Object Length -Sum).Sum
if ((Get-ItemProperty -LiteralPath $userKey).InstallLocation.TrimEnd('\') -ne $target) { throw 'Uninstall entry escaped isolated path.' }
$uninstall = Start-Process (Join-Path $target 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
if ($uninstall.ExitCode -ne 0 -or (Test-Path -LiteralPath (Join-Path $target 'SensorPanel.exe')) -or (Test-Path -LiteralPath $userKey)) { throw 'Isolated uninstall did not finish cleanly.' }
if ((Get-FileHash -LiteralPath (Join-Path $production 'SensorPanel.exe')).Hash -ne $productionHash -or $scheduler.GetFolder('\').GetTask($taskName).Xml -ne $beforeTask) { throw 'Production app or startup task changed.' }
[pscustomobject]@{Version=$manifest.Version;InstallerSHA256=$manifest.InstallerSHA256;InstalledFileDigests=$manifest.Files.Count;InstalledMiB=[math]::Round($installedBytes/1MB,2);LegacyRuntimeCleanup=$true;SharedRuntimeAppCheck=$true;UninstallPassed=$true;ProductionAndStartupPreserved=$true;LivePanelLaunched=$false} | ConvertTo-Json | Set-Content -LiteralPath '.\artifacts\installer\shared-install-validation.json' -Encoding UTF8
Write-Host 'PASS: shared-runtime install, 24 file digests, legacy runtime cleanup, application resource checks, uninstall and production preservation.'
