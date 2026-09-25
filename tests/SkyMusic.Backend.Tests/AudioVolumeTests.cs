// 模块：验证真正送往声卡的 PCM 增益，覆盖拖动音量、静音及重新创建输出链路。
using NAudio.Wave;
using SkyMusic.Infrastructure.Playback;

namespace SkyMusic.Backend.Tests;

public sealed class AudioVolumeTests
{
    [Fact]
    public void VolumeChangesScalePcmAndSurviveOutputRecreation()
    {
        using var player = new FfmpegAudioPlayer("ffmpeg.exe");
        var samples = new float[4];
        var output = player.CreateVolumeProvider(new ConstantPcm());
        output.Read(samples, 0, samples.Length);
        Assert.All(samples, sample => Assert.Equal(0.5f, sample));
        player.Volume = 0.25;
        output.Read(samples, 0, samples.Length);
        Assert.All(samples, sample => Assert.Equal(0.125f, sample));
        output = player.CreateVolumeProvider(new ConstantPcm());
        output.Read(samples, 0, samples.Length);
        Assert.All(samples, sample => Assert.Equal(0.125f, sample));
        player.Volume = 0;
        output.Read(samples, 0, samples.Length);
        Assert.All(samples, sample => Assert.Equal(0, sample));
    }

    private sealed class ConstantPcm : IWaveProvider
    {
        public WaveFormat WaveFormat { get; } = new(48_000, 16, 2);
        public int Read(byte[] buffer, int offset, int count)
        {
            for (var i = offset; i < offset + count; i += 2) { buffer[i] = 0; buffer[i + 1] = 64; }
            return count;
        }
    }
}
