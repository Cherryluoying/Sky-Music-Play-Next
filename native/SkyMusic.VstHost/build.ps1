# 模块：SkyMusic.VstHost 原生构建 build
param(
    [string]$Configuration = "Release",
    [string]$SdkRoot = ""
)

$ErrorActionPreference = "Stop"
$nativeRoot = Split-Path -Parent $PSScriptRoot
$solutionRoot = Split-Path -Parent $nativeRoot
if ([string]::IsNullOrWhiteSpace($SdkRoot)) {
    $SdkRoot = Join-Path $solutionRoot "third_party\vst3sdk"
}

$SdkRoot = [System.IO.Path]::GetFullPath($SdkRoot)
$buildDirectory = Join-Path $PSScriptRoot "build-vs18"
if (-not (Test-Path -LiteralPath (Join-Path $SdkRoot "CMakeLists.txt"))) {
    throw "VST3 SDK not found: $SdkRoot"
}

$cmakeCommand = (Get-Command cmake -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($cmakeCommand)) {
    $vswhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $visualStudio = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        $bundledCmake = Join-Path $visualStudio "Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
        if (Test-Path -LiteralPath $bundledCmake) {
            $cmakeCommand = $bundledCmake
        }
    }
}
if ([string]::IsNullOrWhiteSpace($cmakeCommand)) {
    throw "CMake was not found"
}

& $cmakeCommand -S $PSScriptRoot -B $buildDirectory -G "Visual Studio 18 2026" -A x64 "-DSKYMUSIC_VST3_SDK_ROOT=$SdkRoot"
if ($LASTEXITCODE -ne 0) { throw "CMake configuration failed with exit code $LASTEXITCODE" }
& $cmakeCommand --build $buildDirectory --config $Configuration --target SkyMusic.VstHost
if ($LASTEXITCODE -ne 0) { throw "VST3 host build failed with exit code $LASTEXITCODE" }

$outputDirectory = Join-Path $PSScriptRoot "bin\$Configuration"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $buildDirectory "$Configuration\SkyMusic.VstHost.exe") `
    -Destination (Join-Path $outputDirectory "SkyMusic.VstHost.exe") -Force
