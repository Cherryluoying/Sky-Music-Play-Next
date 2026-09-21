// 模块：SkyMusic.Backend.Tests 后端测试 TickTimeConverterTests
using SkyMusic.Core.Projects;

namespace SkyMusic.Backend.Tests;

public sealed class TickTimeConverterTests
{
    [Fact]
    public void ConvertsAcrossTempoChanges()
    {
        var converter = new TickTimeConverter(480,
        [
            new TempoChange(0, 500_000),
            new TempoChange(480, 1_000_000)
        ]);

        Assert.Equal(500_000, converter.TickToMicroseconds(480));
        Assert.Equal(1_500_000, converter.TickToMicroseconds(960));
        Assert.Equal(720, converter.MicrosecondsToTick(1_000_000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(120)]
    [InlineData(480)]
    [InlineData(960)]
    [InlineData(4_321)]
    public void RoundTripsTicks(long tick)
    {
        var converter = new TickTimeConverter(480,
        [
            new TempoChange(0, 500_000),
            new TempoChange(960, 400_000),
            new TempoChange(1_920, 750_000)
        ]);

        var result = converter.MicrosecondsToTick(converter.TickToMicroseconds(tick));

        Assert.InRange(result, tick - 1, tick + 1);
    }
}
