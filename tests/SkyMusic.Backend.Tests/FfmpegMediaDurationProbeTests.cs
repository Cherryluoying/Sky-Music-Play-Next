// 模块：SkyMusic.Backend.Tests FFprobe 路径回退测试
using SkyMusic.Infrastructure.Media;

namespace SkyMusic.Backend.Tests;

public sealed class FfmpegMediaDurationProbeTests
{
    [Fact]
    public void FindFfprobe_FallsBackToBundledToolchainWhenConfiguredFfmpegHasNoProbe()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"skymusic-ffprobe-{Guid.NewGuid():N}");
        var configuredDirectory = Path.Combine(directory, "PianoTrans", "ffmpeg");
        var applicationDirectory = Path.Combine(directory, "SkyMusicPlay.Next", "src", "App", "bin");
        var bundledDirectory = Path.Combine(directory, "SkyMusicPlay.Next", "ffmpeg", "bin");
        Directory.CreateDirectory(configuredDirectory);
        Directory.CreateDirectory(applicationDirectory);
        Directory.CreateDirectory(bundledDirectory);
        var ffmpegPath = Path.Combine(configuredDirectory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        var ffprobePath = Path.Combine(bundledDirectory, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

        try
        {
            File.WriteAllBytes(ffmpegPath, [0]);
            File.WriteAllBytes(ffprobePath, [0]);

            var resolved = FfmpegMediaDurationProbe.FindFfprobe(ffmpegPath, applicationDirectory);

            Assert.Equal(Path.GetFullPath(ffprobePath), resolved);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
