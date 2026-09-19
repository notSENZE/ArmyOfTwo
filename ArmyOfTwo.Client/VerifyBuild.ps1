param([string]$SPTPath = 'S:\SPT')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$serverProject = [xml](Get-Content -Raw -LiteralPath (Join-Path $root 'ArmyOfTwo.Server\ArmyOfTwo.Server.csproj'))
$prepatchProject = [xml](Get-Content -Raw -LiteralPath (Join-Path $root 'ArmyOfTwo.Prepatch\ArmyOfTwo.Prepatch.csproj'))
$clientProject = [xml](Get-Content -Raw -LiteralPath (Join-Path $root 'ArmyOfTwo.Client\ArmyOfTwo.Client.csproj'))
$version = $serverProject.Project.PropertyGroup.Version
if ($prepatchProject.Project.PropertyGroup.Version -ne $version -or
    $clientProject.Project.PropertyGroup.Version -ne $version) {
    throw 'The three project versions do not match.'
}

Add-Type -Path (Join-Path $SPTPath 'BepInEx\core\Mono.Cecil.dll')
$build = Join-Path $root 'Build'
$clientPath = Join-Path $build 'BepInEx\plugins\ArmyOfTwo\ArmyOfTwo.Client.dll'
$prepatchPath = Join-Path $build 'BepInEx\patchers\ArmyOfTwo\ArmyOfTwo.Prepatch.dll'
$serverPath = Join-Path $build 'SPT_Runtime\user\mods\ArmyOfTwo\ArmyOfTwo.Server.dll'
$guid = 'spt.senze.armyoftwo'

foreach ($item in @(
    @{ Path = $clientPath; Type = 'ArmyOfTwo.Client.Plugin' },
    @{ Path = $prepatchPath; Type = 'ArmyOfTwo.Prepatch.Plugin' }
)) {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($item.Path)
    try {
        $plugin = $assembly.MainModule.Types | Where-Object FullName -eq $item.Type
        $attribute = $plugin.CustomAttributes | Where-Object {
            $_.AttributeType.FullName -eq 'BepInEx.BepInPlugin'
        }
        if ($null -eq $attribute -or
            $attribute.ConstructorArguments[0].Value -ne $guid -or
            $attribute.ConstructorArguments[2].Value -ne $version) {
            throw "$($item.Type) declares the wrong GUID or version."
        }
        [void][version]::Parse($attribute.ConstructorArguments[2].Value)
    }
    finally {
        $assembly.Dispose()
    }
}

$server = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($serverPath)
try {
    $modInfo = $server.MainModule.Types | Where-Object FullName -eq 'ArmyOfTwo.Server.ModInfo'
    $serverVersion = $modInfo.Fields | Where-Object Name -eq 'Version'
    $metadata = $server.MainModule.Types | Where-Object FullName -eq 'ArmyOfTwo.Server.ModMetadata'
    $metadataPatch = $metadata.Methods | Where-Object Name -eq '.ctor' |
        ForEach-Object { $_.Body.Instructions } |
        Where-Object { $_.OpCode.Name -eq "ldc.i4.$($version.Split('.')[2])" }
    if ($serverVersion.Constant -ne $version -or $null -eq $metadataPatch -or
        'SPTarkov.Server.Core.Models.Spt.Mod.IModMetadata' -notin
            @($metadata.Interfaces | ForEach-Object { $_.InterfaceType.FullName })) {
        throw 'The server DLL metadata does not match the project version.'
    }
}
finally {
    $server.Dispose()
}

Write-Output "Army of Two ${version}: all three DLLs and package paths verified."
