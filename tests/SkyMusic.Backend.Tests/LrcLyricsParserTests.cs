// 模块：SkyMusic.Backend.Tests 本地歌词解析测试
using SkyMusic.Core.Lyrics;

namespace SkyMusic.Backend.Tests;

public sealed class LrcLyricsParserTests
{
    [Fact]
    public void Parse_SortsMultipleTimestampTagsAndIgnoresMetadata()
    {
        const string content = """
            [ar:测试歌手]
            [00:12.50][00:20.125]第二句
            [00:03.2]第一句
            """;

        var lines = LrcLyricsParser.Parse(content);

        Assert.Equal(3, lines.Count);
        Assert.Equal(TimeSpan.FromSeconds(3.2), lines[0].Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(12.5), lines[1].Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(20.125), lines[2].Timestamp);
        Assert.Equal("第二句", lines[2].Text);
    }

    [Fact]
    public void Parse_PlainTextCreatesClickableFiveSecondTimeline()
    {
        var lines = LrcLyricsParser.Parse("第一句\n第二句\n第三句");

        Assert.Equal(3, lines.Count);
        Assert.Equal(TimeSpan.Zero, lines[0].Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(5), lines[1].Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(10), lines[2].Timestamp);
    }
}
