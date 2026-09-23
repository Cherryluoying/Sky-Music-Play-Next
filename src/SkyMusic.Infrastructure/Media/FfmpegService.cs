// 模块：SkyMusic.Infrastructure 媒体工具 FfmpegService
using System.Diagnostics;
using SkyMusic.Core.ExternalTools;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Media;

public sealed class FfmpegService(
    string startDirectory,
    string? pianoTransDirectory = null,
    TimeSpan? probeTimeout = null) : IFfmpegService
{
    private readonly TimeSpan _probeTimeout = probeTimeout ?? TimeSpan.FromSeconds(5);

    public string? Locate(string? configuredPath = null)
    {
        foreach (var candidate in EnumerateCandidates(configuredPath))
        {
            try
            {
                var path = Path.GetFullPath(candidate.Path);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }

        return null;
    }

    // 依次探测配置路径、环境变量、应用内置工具链和系统 PATH。
    public async ValueTask<ExternalToolStatus> ProbeAsync(
        string? configuredPath = null,
        CancellationToken cancellationToken = default)
    {
        var candidate = FindCandidate(configuredPath);
        if (candidate is null)
        {
            return new ExternalToolStatus(false, null, "未找到 FFmpeg");
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = candidate.Value.Path,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add("-version");

        try
        {
            if (!process.Start())
            {
                return new ExternalToolStatus(false, candidate.Value.Path, "FFmpeg 无法启动", Source: candidate.Value.Source);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = new CancellationTokenSource(_probeTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            await process.WaitForExitAsync(linked.Token);
            var output = await outputTask;
            var error = await errorTask;
            var firstLine = (string.IsNullOrWhiteSpace(output) ? error : output)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            return process.ExitCode == 0
                ? new ExternalToolStatus(true, candidate.Value.Path, "FFmpeg 已就绪", firstLine, candidate.Value.Source)
                : new ExternalToolStatus(false, candidate.Value.Path, $"FFmpeg 返回代码 {process.ExitCode}", firstLine, candidate.Value.Source);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new ExternalToolStatus(false, candidate.Value.Path, "FFmpeg 检测超时", Source: candidate.Value.Source);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return new ExternalToolStatus(false, candidate.Value.Path, $"FFmpeg 不可用: {exception.Message}", Source: candidate.Value.Source);
        }
    }

    private Candidate? FindCandidate(string? configuredPath)
    {
        foreach (var candidate in EnumerateCandidates(configuredPath))
        {
            try
            {
                var path = Path.GetFullPath(candidate.Path);
                if (File.Exists(path))
                {
                    return candidate with { Path = path };
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }

        return null;
    }

    // 按优先级生成去重后的 FFmpeg 候选路径
    private IEnumerable<Candidate> EnumerateCandidates(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return new Candidate(ResolveExecutable(configuredPath), "用户设置");
        }

        var environmentPath = Environment.GetEnvironmentVariable("SKYMUSIC_FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            yield return new Candidate(ResolveExecutable(environmentPath), "环境变量");
        }

        var executableName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var directoryInfo = new DirectoryInfo(Path.GetFullPath(startDirectory));
        for (var depth = 0; depth < 10 && directoryInfo is not null; depth++, directoryInfo = directoryInfo.Parent)
        {
            yield return new Candidate(
                Path.Combine(directoryInfo.FullName, "ffmpeg", "bin", executableName),
                "应用内置 FFmpeg");
            yield return new Candidate(
                Path.Combine(directoryInfo.FullName, "PianoTrans-v1.0", "ffmpeg", executableName),
                "PianoTrans 扩展");
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return new Candidate(Path.Combine(directory, executableName), "系统 PATH");
        }

        if (!string.IsNullOrWhiteSpace(pianoTransDirectory))
        {
            yield return new Candidate(Path.Combine(pianoTransDirectory, "ffmpeg", executableName), "PianoTrans 扩展");
        }
    }

    private static string ResolveExecutable(string path)
    {
        var trimmed = path.Trim().Trim('"');
        return Directory.Exists(trimmed)
            ? Path.Combine(trimmed, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg")
            : trimmed;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private readonly record struct Candidate(string Path, string Source);
}
