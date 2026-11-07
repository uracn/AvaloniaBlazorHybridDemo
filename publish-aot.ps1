<#
.SYNOPSIS
    Publish AvaloniaBlazorHybridDemo as a NativeAOT single executable (Windows x64).

.DESCRIPTION
    NativeAOT needs the MSVC linker (link.exe) and the Windows SDK import libs on
    LIB/INCLUDE. A plain "dotnet publish -p:PublishAot=true" from a normal shell
    often fails with LNK1104 because those are only set inside a Developer Prompt.
    This script sets them up automatically.

    Requirements: Visual Studio with "Desktop development with C++" workload,
    Windows 10/11 SDK, .NET 10 SDK, WebView2 Runtime (evergreen).

.EXAMPLE
    .\publish-aot.ps1
    .\publish-aot.ps1 -Runtime win-arm64 -Output bin\publish-aot-arm64
#>
param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $Output = "bin/publish-aot"
)

$ErrorActionPreference = "Stop"

# --- Locate the newest Visual Studio C++ toolchain -----------------------------
$vsRoots = @(
    "C:\Program Files\Microsoft Visual Studio\18\Community",
    "C:\Program Files\Microsoft Visual Studio\18\Professional",
    "C:\Program Files\Microsoft Visual Studio\18\Enterprise",
    "C:\Program Files\Microsoft Visual Studio\2022\Community",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise"
)
$vsRoot = $vsRoots | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $vsRoot) { throw "Visual Studio with C++ tools not found. Edit `$vsRoots in this script." }

$msvc = (Get-ChildItem (Join-Path $vsRoot 'VC\Tools\MSVC') -Directory |
         Sort-Object Name -Descending | Select-Object -First 1).FullName
$kits = 'C:\Program Files (x86)\Windows Kits\10'
$kitsLib = (Get-ChildItem (Join-Path $kits 'Lib') -Directory |
            Sort-Object Name -Descending | Select-Object -First 1).FullName
$kitsInc = (Get-ChildItem (Join-Path $kits 'Include') -Directory |
            Sort-Object Name -Descending | Select-Object -First 1).FullName

$arch = if ($Runtime -like '*x64*') { 'x64' } elseif ($Runtime -like '*arm64*') { 'arm64' } else { 'x86' }
$env:LIB = "$msvc\lib\$arch;$kitsLib\um\$arch;$kitsLib\ucrt\$arch"
$env:INCLUDE = "$msvc\include;$kitsInc\um;$kitsInc\ucrt;$kitsInc\shared;$kitsInc\winrt"
$env:PATH = "$msvc\bin\Hostx64\$arch;$env:PATH"

$project = Join-Path $PSScriptRoot 'AvaloniaBlazorHybridDemo.csproj'
$outDir = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $PSScriptRoot $Output }

Write-Host "MSVC : $msvc"
Write-Host "SDK  : $kitsLib"
Write-Host "OUT  : $outDir"
Write-Host ""

# IlcUseEnvironmentalTools tells ILC to take link.exe / lib.exe from the PATH and
# LIB/INCLUDE set above instead of running findvcvarsall.bat. That batch script
# probes Visual Studio through vswhere/reg.exe and fails with "Platform linker not
# found" on machines where the C++ toolchain is installed but not fully registered.
dotnet publish $project -c $Configuration -r $Runtime `
    -p:EnableWindowsAot=true `
    -p:JsonSerializerIsReflectionEnabledByDefault=true `
    -p:IlcUseEnvironmentalTools=true `
    -o $outDir --nologo

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$exe = Join-Path $outDir 'AvaloniaBlazorHybridDemo.exe'
if (Test-Path $exe) {
    $sizeMB = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host ""
    Write-Host "NativeAOT publish succeeded: $exe ($sizeMB MB)"
    Write-Host "Run it with:  & '$exe'"
}
