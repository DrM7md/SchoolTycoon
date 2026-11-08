# Builds Core for netstandard2.1 and copies it plus its runtime dependency DLLs into the Unity project.
# The dependency list comes from the build's deps.json, so it follows Core's PackageReferences.
# System.Runtime.CompilerServices.Unsafe is copied: the URP 17.0.4 package set does not ship it (verified on first open,
# 2026-11-08). If a later package (e.g. com.unity.collections) ships its own copy, add it to $skip. See Docs/Development.md.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$core = Join-Path $root 'src/SchoolDirectorTycoon.Core'
$target = Join-Path $root 'Unity/SchoolDirectorTycoon/Assets/Plugins/Core'
$skip = @()

dotnet build $core -c Release -f netstandard2.1 -m:1 -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw "Core build failed ($LASTEXITCODE)." }

$bin = Join-Path $core 'bin/Release/netstandard2.1'
$deps = Get-Content (Join-Path $bin 'SchoolDirectorTycoon.Core.deps.json') -Raw | ConvertFrom-Json
$packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget/packages' }

New-Item -ItemType Directory -Force $target | Out-Null
Get-ChildItem $target -Filter *.dll | Remove-Item -Force
Copy-Item (Join-Path $bin 'SchoolDirectorTycoon.Core.dll') $target

$targetName = $deps.runtimeTarget.name
foreach ($library in $deps.targets.$targetName.PSObject.Properties) {
    $name, $version = $library.Name -split '/'
    if ($name -eq 'SchoolDirectorTycoon.Core' -or $skip -contains $name -or -not $library.Value.runtime) { continue }
    foreach ($dll in $library.Value.runtime.PSObject.Properties.Name) {
        $source = Join-Path $packages "$($name.ToLowerInvariant())/$version/$dll"
        if (-not (Test-Path $source)) { throw "Missing $source" }
        Copy-Item $source $target
    }
}
Get-ChildItem $target -Filter *.dll | ForEach-Object { $_.Name }
