// 模块：SkyMusic.Infrastructure FFprobe 媒体时长探测
using System.Diagnostics;
using System.Globalization;

namespace SkyMusic.Infrastructure.Media;

internal static class FfmpegMediaDurationProbe
{
    public static TimeSpan Probe(string? ffmpegPath, string mediaPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            return TimeSpan.Zero;
        }

        var ffprobePath = FindFfprobe(ffmpegPath, AppContext.BaseDirectory);
        if (ffprobePath is null)
        {
            return TimeSpan.Zero;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add("-v");
        process.StartInfo.ArgumentList.Add("error");
        process.StartInfo.ArgumentList.Add("-show_entries");
        process.StartInfo.ArgumentList.Add("format=duration");
        process.StartInfo.ArgumentList.Add("-of");
        process.StartInfo.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        process.StartInfo.ArgumentList.Add(Path.GetFullPath(mediaPath));

        try
        {
            if (!process.Start())
            {
                return TimeSpan.Zero;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5_000) || process.ExitCode != 0)
            {
                TryKill(process);
                return TimeSpan.Zero;
            }

            return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) &&
                   double.IsFinite(seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.Zero;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return TimeSpan.Zero;
        }
    }

    // PianoTrans 可能只带 ffmpeg；同时从所选工具和应用目录查找完整的 ffmpeg/bin 工具链。
    internal static string? FindFfprobe(string ffmpegPath, string applicationDirectory)
    {
        var executableName = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
        var ffmpegDirectory = Path.GetDirectoryName(Path.GetFullPath(ffmpegPath));
        if (ffmpegDirectory is null)
        {
            return null;
        }

        var candidates = new List<string>();
        foreach (var startDirectory in new[] { ffmpegDirectory, Path.GetFullPath(applicationDirectory) })
        {
            var directory = new DirectoryInfo(startDirectory);
            candidates.Add(Path.Combine(directory.FullName, executableName));
            for (var depth = 0; depth < 10 && directory is not null; depth++, directory = directory.Parent)
            {
                candidates.Add(Path.Combine(directory.FullName, "ffmpeg", "bin", executableName));
                candidates.Add(Path.Combine(directory.FullName, "ffmpeg", executableName));
            }
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
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
}
