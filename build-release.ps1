# Module: MaoJuMi Music Windows release builder
[CmdletBinding()]
param(
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",

    [switch]$SkipTests,

    [switch]$NoArchive,

    [string]$FfmpegDownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $repositoryRoot "SkyMusicPlay.Next.sln"
$appProjectPath = Join-Path $repositoryRoot "src\SkyMusic.App\SkyMusic.App.csproj"
$testProjectPath = Join-Path $repositoryRoot "tests\SkyMusic.Backend.Tests\SkyMusic.Backend.Tests.csproj"
$releaseRoot = Join-Path $repositoryRoot "artifacts\release"
$packageName = "MaoJuMiMusic-$Runtime"
$publishDirectory = Join-Path $releaseRoot $packageName
$archivePath = Join-Path $releaseRoot "$packageName.zip"

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-FfmpegBinDirectory {
    $candidatePaths = [System.Collections.Generic.List[string]]::new()

    if (-not [string]::IsNullOrWhiteSpace($env:SKYMUSIC_FFMPEG_PATH)) {
        $configuredPath = $env:SKYMUSIC_FFMPEG_PATH.Trim().Trim('"')
        if ([System.IO.Path]::GetExtension($configuredPath) -eq ".exe") {
            $candidatePaths.Add((Split-Path -Parent $configuredPath))
        } else {
            $candidatePaths.Add($configuredPath)
            $candidatePaths.Add((Join-Path $configuredPath "bin"))
        }
    }

    # Support normal clones and the workspace/github sibling directory layout.
    $candidatePaths.Add((Join-Path $repositoryRoot "ffmpeg\bin"))
    $candidatePaths.Add((Join-Path $repositoryRoot "..\ffmpeg\bin"))
    $candidatePaths.Add((Join-Path $repositoryRoot "..\..\ffmpeg\bin"))
    $candidatePaths.Add((Join-Path $repositoryRoot "..\..\SkyMusicPlay.Next\ffmpeg\bin"))

    foreach ($candidatePath in $candidatePaths) {
        try {
            $fullPath = [System.IO.Path]::GetFullPath($candidatePath)
        } catch {
            continue
        }

        if ((Test-Path -LiteralPath (Join-Path $fullPath "ffmpeg.exe") -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $fullPath "ffprobe.exe") -PathType Leaf)) {
            return $fullPath
        }
    }

    return $null
}

function Install-FfmpegToolchain {
    param([Parameter(Mandatory)][string]$DownloadUrl)

    $toolsRoot = Join-Path $repositoryRoot "artifacts\tools"
    $archiveFile = Join-Path $toolsRoot "ffmpeg.zip"
    $extractRoot = Join-Path $toolsRoot "ffmpeg"
    New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null

    if (Test-Path -LiteralPath $extractRoot) {
        Remove-Item -LiteralPath $extractRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $archiveFile) {
        Remove-Item -LiteralPath $archiveFile -Force
    }

    Write-Host "Downloading FFmpeg from $DownloadUrl" -ForegroundColor Cyan
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $archiveFile -UseBasicParsing
    Expand-Archive -LiteralPath $archiveFile -DestinationPath $extractRoot -Force
    Remove-Item -LiteralPath $archiveFile -Force

    $ffmpegExecutable = Get-ChildItem -LiteralPath $extractRoot -Filter "ffmpeg.exe" -File -Recurse |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.DirectoryName "ffprobe.exe") -PathType Leaf } |
        Select-Object -First 1
    if ($null -eq $ffmpegExecutable) {
        throw "The downloaded archive does not contain ffmpeg.exe and ffprobe.exe in the same directory."
    }

    return $ffmpegExecutable.DirectoryName
}

function Copy-FfmpegRuntime {
    param(
        [Parameter(Mandatory)][string]$SourceBinDirectory,
        [Parameter(Mandatory)][string]$DestinationRoot
    )

    $destinationBin = Join-Path $DestinationRoot "ffmpeg\bin"
    New-Item -ItemType Directory -Path $destinationBin -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $SourceBinDirectory "ffmpeg.exe") -Destination $destinationBin -Force
    Copy-Item -LiteralPath (Join-Path $SourceBinDirectory "ffprobe.exe") -Destination $destinationBin -Force

    $sourceRoot = Split-Path -Parent $SourceBinDirectory
    foreach ($documentName in @("LICENSE", "README.txt")) {
        $documentPath = Join-Path $sourceRoot $documentName
        if (Test-Path -LiteralPath $documentPath -PathType Leaf) {
            Copy-Item -LiteralPath $documentPath -Destination (Join-Path $DestinationRoot "ffmpeg") -Force
        }
    }
}

try {
    Write-Host "[1/5] Checking build environment" -ForegroundColor Cyan
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet was not found. Install the .NET SDK specified by global.json."
    }
    if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
        throw "Solution not found: $solutionPath"
    }
    $ffmpegBinDirectory = Get-FfmpegBinDirectory
    if ([string]::IsNullOrWhiteSpace($ffmpegBinDirectory)) {
        $ffmpegBinDirectory = Install-FfmpegToolchain -DownloadUrl $FfmpegDownloadUrl
    }
    Write-Host "FFmpeg: $ffmpegBinDirectory"

    Write-Host "[2/5] Restoring NuGet packages" -ForegroundColor Cyan
    Invoke-DotNet @("restore", $solutionPath)

    Write-Host "[3/5] Building and testing" -ForegroundColor Cyan
    Invoke-DotNet @("build", $solutionPath, "-c", "Release", "--no-restore")
    if (-not $SkipTests) {
        Invoke-DotNet @("test", $testProjectPath, "-c", "Release", "--no-build", "--no-restore")
    } else {
        Write-Host "Tests were skipped." -ForegroundColor Yellow
    }

    Write-Host "[4/5] Publishing self-contained Windows application" -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }
    Invoke-DotNet @(
        "publish", $appProjectPath,
        "-c", "Release",
        "-r", $Runtime,
        "--self-contained", "true",
        "-o", $publishDirectory,
        "-p:DebugType=None",
        "-p:DebugSymbols=false"
    )

    Write-Host "[5/5] Bundling FFmpeg" -ForegroundColor Cyan
    Copy-FfmpegRuntime -SourceBinDirectory $ffmpegBinDirectory -DestinationRoot $publishDirectory

    if (-not $NoArchive) {
        Write-Host "Creating ZIP archive. FFmpeg is large, so this can take a while..." -ForegroundColor Cyan
        Compress-Archive -LiteralPath $publishDirectory -DestinationPath $archivePath -CompressionLevel Optimal
    }

    $publishSize = (Get-ChildItem -LiteralPath $publishDirectory -File -Recurse |
        Measure-Object -Property Length -Sum).Sum
    Write-Host ""
    Write-Host "Release completed" -ForegroundColor Green
    Write-Host "Directory: $publishDirectory"
    Write-Host ("Size: {0:N2} MB" -f ($publishSize / 1MB))
    if (-not $NoArchive) {
        Write-Host "Archive: $archivePath"
    }
} catch {
    Write-Error $_
    exit 1
}
