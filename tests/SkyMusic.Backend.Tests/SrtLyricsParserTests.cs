// 模块：SRT 歌词回归；覆盖双语、时间精度、损坏块和从文件导入后的格式识别。
using SkyMusic.Core.Lyrics;

namespace SkyMusic.Backend.Tests;

public sealed class SrtLyricsParserTests
{
    [Fact]
    public void ParsesHoursMillisecondsAndBilingualTextInOrder()
    {
        var lines = SrtLyricsParser.Parse("\uFEFF1\r\n01:02:03,125 --> 01:02:05,000\r\n<i>Hello &amp; goodbye</i>\r\n你好，再见\r\n\r\n2\r\n00:00:01.250 --> 00:00:02.500\r\nFirst line");
        Assert.Equal(2, lines.Count);
        Assert.Equal(TimeSpan.FromSeconds(1.25), lines[0].Timestamp);
        Assert.Equal(TimeSpan.FromMilliseconds(3723125), lines[1].Timestamp);
        Assert.Equal("Hello & goodbye", lines[1].Text);
        Assert.Equal("你好，再见", lines[1].Translation);
    }

    [Fact]
    public void SkipsMalformedReversedAndEmptyCuesWithoutTreatingNumbersAsLyrics()
    {
        const string content = "1\n00:70:00,000 --> 00:70:01,000\n坏时间\n\n2\n00:00:03,000 --> 00:00:01,000\n反向\n\n3\n00:00:04,000 --> 00:00:05,000\n\n4\n00:00:06,000 --> 00:00:07,000\n正常";
        var line = Assert.Single(SrtLyricsParser.Parse(content));
        Assert.Equal("正常", line.Text);
        Assert.Equal(TimeSpan.FromSeconds(6), line.Timestamp);
        Assert.Empty(SrtLyricsParser.Parse("1\n损坏的文件"));
    }

    [Fact]
    public void MergesSeparateTranslationCuesAtSameTimestamp()
    {
        var line = Assert.Single(SrtLyricsParser.Parse("1\n00:00:01,000 --> 00:00:03,000\nHello\n\n2\n00:00:01,000 --> 00:00:03,000\n你好"));
        Assert.Equal("Hello", line.Text);
        Assert.Equal("你好", line.Translation);
    }

    [Fact]
    public async Task FileImportRecognizesUppercaseSrtAndUtf16Bom()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".SRT");
        try
        {
            await File.WriteAllTextAsync(path, "1\n00:00:09,500 --> 00:00:11,000\n歌词", System.Text.Encoding.Unicode);
            var line = Assert.Single(await LrcLyricsParser.ParseFileAsync(path));
            Assert.Equal(TimeSpan.FromSeconds(9.5), line.Timestamp);
            Assert.Equal("歌词", line.Text);
        }
        finally { File.Delete(path); }
    }
}
