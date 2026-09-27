// 模块：与 music_web_gateway 双语歌词格式的兼容回归，防止翻译继续留在原文旁边。
using SkyMusic.Core.Lyrics;
using SkyMusic.Core.Models;

namespace SkyMusic.Backend.Tests;

public sealed class BilingualLyricsTests
{
    [Theory]
    [InlineData("君を待っている（我在等你）", "君を待っている", "我在等你")]
    [InlineData("君を待っている (我在等你)", "君を待っている", "我在等你")]
    [InlineData("Hello（你好）", "Hello", "你好")]
    public void ExtractsTrailingChineseTranslation(string input, string original, string translation)
    {
        var local = Assert.Single(LrcLyricsParser.Parse("[00:01.50]" + input));
        Assert.Equal(original, local.Text);
        Assert.Equal(translation, local.Translation);
        Assert.Equal(TimeSpan.FromSeconds(1.5), local.Timestamp);
        // 已保存或云端加载的合并文本也必须生效，且二次处理结果保持一致。
        var cached = Assert.Single(LyricLines.Normalize([new LyricLine(local.Timestamp, input)]));
        Assert.Equal(local, cached);
        Assert.Equal(local, Assert.Single(LyricLines.Normalize([cached])));
    }

    [Theory]
    [InlineData("[00:01]Hello\n[00:01][tr]你好")]
    [InlineData("[00:01][TR]你好\n[00:01]Hello")]
    [InlineData("[tr][00:01]你好\n[00:01]Hello")]
    public void TranslationMarkerAssociatesByTimestampRegardlessOfOrder(string lrc)
    {
        var line = Assert.Single(LrcLyricsParser.Parse(lrc));
        Assert.Equal("Hello", line.Text);
        Assert.Equal("你好", line.Translation);
    }

    [Theory]
    [InlineData("Hello (live)")]
    [InlineData("（纯中文整句）")]
    [InlineData("AC/DC")]
    [InlineData("hello（你好）again")]
    public void PreservesTextWithoutSupportedTrailingTranslation(string text)
    {
        var line = Assert.Single(LyricLines.Normalize([new LyricLine(TimeSpan.Zero, text)]));
        Assert.Equal(text, line.Text);
        Assert.Null(line.Translation);
    }

    [Fact]
    public void UnmarkedJapaneseChineseDocumentPreservesSpacesInsideTranslation()
    {
        var lines = LrcLyricsParser.Parse("""
            [00:00]作词 : 测试
            [00:01]青い 空を見ている 看着蓝色的天空
            [00:02]雨が止んだ 雨已经停了
            [00:03]君を待っている 我在等你 直到明天
            [00:04]風が吹いている 此刻微风轻拂
            """);
        Assert.Null(lines[0].Translation);
        Assert.Equal("青い 空を見ている", lines[1].Text);
        Assert.Equal("看着蓝色的天空", lines[1].Translation);
        Assert.Equal("君を待っている", lines[3].Text);
        Assert.Equal("我在等你 直到明天", lines[3].Translation);
        Assert.Equal("此刻微风轻拂", lines[4].Translation);
        Assert.Equal(lines, LyricLines.Normalize(lines));
    }

    [Fact]
    public void DoesNotTreatOrdinaryJapaneseSpacesAsTranslations()
    {
        var lines = LrcLyricsParser.Parse("""
            [00:01]青い空 遠い街
            [00:02]雨が止んだ 東京
            [00:03]君を待つ 世界
            [00:04]風が吹く 季節
            """);
        Assert.All(lines, line => Assert.Null(line.Translation));
        Assert.Equal("雨が止んだ 東京", lines[1].Text);
        Assert.Null(Assert.Single(LrcLyricsParser.Parse("[00:01]君を待っている 我在等你")).Translation);
    }

    [Fact]
    public void InlineAndExplicitTranslationsDoNotDuplicate()
    {
        var line = Assert.Single(LyricLines.Normalize([new LyricLine(TimeSpan.Zero, "Hello（你好）", "你好")]));
        Assert.Equal("Hello", line.Text);
        Assert.Equal("你好", line.Translation);
    }
}
