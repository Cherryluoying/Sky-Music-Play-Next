# 模块：SkyMusic.AudioPreview 原生构建 build
param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$cmakeCommand = (Get-Command cmake -ErrorAction Stop).Source

$generators = @(
    @{ Name = "Visual Studio 18 2026"; Directory = "build-vs18" },
    @{ Name = "Visual Studio 17 2022"; Directory = "build-vs17" }
)
$buildDirectory = $null
foreach ($generator in $generators) {
    $candidate = Join-Path $PSScriptRoot $generator.Directory
    & $cmakeCommand -S $PSScriptRoot -B $candidate -G $generator.Name -A x64
    if ($LASTEXITCODE -eq 0) {
        $buildDirectory = $candidate
        break
    }
}
if ([string]::IsNullOrWhiteSpace($buildDirectory)) { throw "No supported Visual Studio C++ generator was found" }
& $cmakeCommand --build $buildDirectory --config $Configuration --target SkyMusic.AudioPreview
if ($LASTEXITCODE -ne 0) { throw "Audio preview build failed with exit code $LASTEXITCODE" }

$outputDirectory = Join-Path $PSScriptRoot "bin\$Configuration"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $buildDirectory "$Configuration\SkyMusic.AudioPreview.dll") `
    -Destination (Join-Path $outputDirectory "SkyMusic.AudioPreview.dll") -Force
