param(
    [string]$TypePattern = 'HardwareMonitor|GAS1AcpiMethodBase|ACPIMethodSelection',
    [string[]]$Assemblies = @('Gigabyte.WmiAcpi.EnvironmentControl.dll', 'Gigabyte.WmiAcpi.dll', 'Gigabyte.EnvironmentControl.Remoting.dll', 'Gigabyte.EnvironmentControl.dll'),
    [string]$MethodPattern = '.',
    [switch]$DumpIL
)
$ErrorActionPreference = 'Stop'
# Run with Windows PowerShell 5.1. Reflection-only inspection never executes vendor code.
$vendorRoot = 'C:\Program Files (x86)\GIGABYTE\EasyTuneEngineService'
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve({
    param($sender, $eventArgs)
    $candidate = Join-Path $vendorRoot ((New-Object Reflection.AssemblyName($eventArgs.Name)).Name + '.dll')
    if (Test-Path -LiteralPath $candidate) { return [Reflection.Assembly]::ReflectionOnlyLoadFrom($candidate) }
    return [Reflection.Assembly]::ReflectionOnlyLoad($eventArgs.Name)
})
$opcodeMap = @{}
[Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static') | ForEach-Object {
    $opcode = $_.GetValue($null)
    $opcodeMap[[int]$opcode.Value -band 65535] = $opcode
}
function Show-MethodIL($method) {
    $body = $method.GetMethodBody()
    if (!$body) { return }
    $bytes = $body.GetILAsByteArray()
    $position = 0
    while ($position -lt $bytes.Length) {
        $offset = $position
        $code = [int]$bytes[$position++]
        if ($code -eq 254) { $code = 65280 + [int]$bytes[$position++] }
        $op = $opcodeMap[$code]
        if (!$op) { throw "Unknown IL opcode $code" }
        $operand = ''
        switch ([string]$op.OperandType) {
            'InlineNone' { }
            'ShortInlineI' { $operand = [int]$bytes[$position]; $position++ }
            'ShortInlineVar' { $operand = [int]$bytes[$position]; $position++ }
            'InlineVar' { $operand = [BitConverter]::ToUInt16($bytes, $position); $position += 2 }
            'ShortInlineBrTarget' { $operand = [int]$bytes[$position]; $position++ }
            'InlineBrTarget' { $operand = [BitConverter]::ToInt32($bytes, $position); $position += 4 }
            'InlineI' { $operand = [BitConverter]::ToInt32($bytes, $position); $position += 4 }
            'InlineI8' { $operand = [BitConverter]::ToInt64($bytes, $position); $position += 8 }
            'ShortInlineR' { $operand = [BitConverter]::ToSingle($bytes, $position); $position += 4 }
            'InlineR' { $operand = [BitConverter]::ToDouble($bytes, $position); $position += 8 }
            'InlineSwitch' { $count = [BitConverter]::ToInt32($bytes, $position); $position += 4 + 4 * $count; $operand = "switch[$count]" }
            default {
                $token = [BitConverter]::ToInt32($bytes, $position); $position += 4
                try {
                    if ([string]$op.OperandType -eq 'InlineString') { $operand = $method.Module.ResolveString($token) }
                    else { $member = $method.Module.ResolveMember($token); $operand = "$($member.DeclaringType.FullName)::$member" }
                } catch { $operand = 'token 0x{0:X8}' -f $token }
            }
        }
        '    {0:X4} {1} {2}' -f $offset, $op.Name, $operand
    }
}
foreach ($file in $Assemblies) {
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $vendorRoot $file))
    "ASSEMBLY $file"
    try { $types = $assembly.GetTypes() } catch [Reflection.ReflectionTypeLoadException] { $types = $_.Exception.Types | Where-Object { $_ } }
    foreach ($type in $types | Where-Object { $_.FullName -match $TypePattern }) {
        "TYPE $($type.FullName)"
        $methods = @($type.GetMethods([Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) + @($type.GetConstructors([Reflection.BindingFlags]'Public,NonPublic,Instance,Static'))
        $methods | Where-Object { $_.Name -match $MethodPattern } | ForEach-Object {
            '  ' + $_.ToString()
            if ($DumpIL) { Show-MethodIL $_ }
        }
        $type.GetFields([Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly') | ForEach-Object {
            $value = if ($_.IsLiteral) { $_.GetRawConstantValue() } else { '' }
            "  FIELD $($_.FieldType.Name) $($_.Name) $value"
        }
    }
}
