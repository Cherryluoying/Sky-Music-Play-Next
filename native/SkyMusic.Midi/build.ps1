# 模块：SkyMusic.Midi 原生构建 build
param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$buildDirectory = Join-Path $PSScriptRoot "build-vs18"
$cmakeCommand = (Get-Command cmake -ErrorAction Stop).Source

& $cmakeCommand -S $PSScriptRoot -B $buildDirectory -G "Visual Studio 18 2026" -A x64
if ($LASTEXITCODE -ne 0) { throw "CMake configuration failed with exit code $LASTEXITCODE" }
& $cmakeCommand --build $buildDirectory --config $Configuration --target SkyMusic.Midi
if ($LASTEXITCODE -ne 0) { throw "Native MIDI build failed with exit code $LASTEXITCODE" }

$outputDirectory = Join-Path $PSScriptRoot "bin\$Configuration"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $buildDirectory "$Configuration\SkyMusic.Midi.dll") `
    -Destination (Join-Path $outputDirectory "SkyMusic.Midi.dll") -Force
