$ErrorActionPreference = 'Stop'
$probe = [ordered]@{ CapturedAt = [DateTimeOffset]::UtcNow.ToString('O'); Elevated = $false; Phase = 'identity'; InstanceName = $null; MethodId = $null; Readings = @(); Error = $null }
try {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $probe.Elevated = ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    $probe.Phase = 'verify class'
    $class = Get-CimClass -Namespace root/WMI -ClassName GSA1_ACPIMethod
    if ($class.CimClassQualifiers['guid'].Value -ne '{DEADBEEF-2001-0000-00A0-C90629100000}') { throw 'Unexpected firmware GUID' }
    $probe.MethodId = $class.CimClassMethods['ZFCGetCurrentTemp'].Qualifiers['WmiMethodId'].Value
    if ($probe.MethodId -ne 293) { throw 'Unexpected firmware method ID' }
    $probe.Phase = 'get active instance'
    $instances = @(Get-CimInstance -Namespace root/WMI -ClassName GSA1_ACPIMethod | Where-Object Active)
    if ($instances.Count -ne 1) { throw "Expected one active firmware instance, found $($instances.Count)" }
    $probe.InstanceName = [string]$instances[0].InstanceName
    $probe.Phase = 'invoke temperature-only query'
    for ($channel = 0; $channel -lt 6; $channel++) {
        try {
            $result = Invoke-CimMethod -InputObject $instances[0] -MethodName ZFCGetCurrentTemp -Arguments @{ id = [byte]$channel }
            if ($null -eq $result.value) { throw 'No temperature byte returned' }
            $probe.Readings += [ordered]@{ Channel = $channel; RawValue = [int]$result.value; Error = $null }
        } catch {
            $probe.Readings += [ordered]@{ Channel = $channel; RawValue = $null; Error = $_.Exception.ToString() }
        }
    }
    $probe.Phase = 'complete'
} catch { $probe.Error = $_.Exception.ToString() }
$artifactDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts'
[IO.Directory]::CreateDirectory($artifactDirectory) | Out-Null
$json = $probe | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText((Join-Path $artifactDirectory 'gigabyte-cim-probe.json'), $json)
$json
Read-Host 'Read-only probe complete. Press Enter to close'
