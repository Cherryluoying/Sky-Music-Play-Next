// 模块：SkyMusic.Backend.Tests 后端测试 MidiScoreImporterTests
using SkyMusic.Infrastructure.Scores;

namespace SkyMusic.Backend.Tests;

public sealed class MidiScoreImporterTests
{
    private static readonly byte[] SingleNoteMidi =
    [
        0x4D, 0x54, 0x68, 0x64,
        0x00, 0x00, 0x00, 0x06,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x60,
        0x4D, 0x54, 0x72, 0x6B,
        0x00, 0x00, 0x00, 0x0C,
        0x00, 0x90, 0x3C, 0x40,
        0x60, 0x80, 0x3C, 0x40,
        0x00, 0xFF, 0x2F, 0x00
    ];

    [Fact]
    public async Task ImportsMetricTimingAndVelocity()
    {
        await using var stream = new MemoryStream(SingleNoteMidi);

        var result = await new MidiScoreImporter().ImportAsync(stream, "single-note.mid");

        Assert.True(result.IsSuccess);
        var note = Assert.Single(result.Score!.Notes);
        Assert.Equal(60, note.MidiNote);
        Assert.Equal(0, note.StartMicroseconds);
        Assert.Equal(500_000, note.DurationMicroseconds);
        Assert.Equal(64, note.Velocity);
    }
}
