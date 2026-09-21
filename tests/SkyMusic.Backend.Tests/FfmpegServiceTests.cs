// 模块：SkyMusic.Backend.Tests 后端测试 FfmpegServiceTests
using SkyMusic.Infrastructure.Media;

namespace SkyMusic.Backend.Tests;

public sealed class FfmpegServiceTests
{
    [Fact]
    public void ConfiguredExecutableHasHighestPriority()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var executable = Path.Combine(directory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        try
        {
            File.WriteAllBytes(executable, [0]);
            var service = new FfmpegService(directory);

            Assert.Equal(Path.GetFullPath(executable), service.Locate(executable));
            Assert.Equal(Path.GetFullPath(executable), service.Locate(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task InvalidConfiguredExecutableReturnsUnavailableStatus()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var executable = Path.Combine(directory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        try
        {
            File.WriteAllBytes(executable, [0]);
            var service = new FfmpegService(directory, Path.Combine(directory, "PianoTrans"));

            var status = await service.ProbeAsync(executable);

            Assert.False(status.IsAvailable);
            Assert.Equal(Path.GetFullPath(executable), status.ExecutablePath);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
