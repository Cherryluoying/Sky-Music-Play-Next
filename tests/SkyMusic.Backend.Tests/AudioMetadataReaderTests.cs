// 模块：音频标签解析回归，覆盖中文标签、音频流补缺及多封面优先级。
using SkyMusic.Infrastructure.Media;

namespace SkyMusic.Backend.Tests;

public sealed class AudioMetadataReaderTests
{
    [Fact]
    public void ReadsCaseInsensitiveTagsAndPrefersEmbeddedFrontCover()
    {
        var metadata = AudioMetadataReader.Parse("""
            {"format":{"duration":"234.56","tags":{"TITLE":"  斜陽  ","ARTIST":"ヨルシカ",
              "ALBUM":"专辑名","COMPOSER":"词曲作者"}},
             "streams":[{"index":0,"codec_type":"audio","tags":{"title":"不覆盖容器标题"}},
              {"index":1,"codec_type":"video","disposition":{"attached_pic":0}},
              {"index":2,"codec_type":"video","disposition":{"attached_pic":1},"tags":{"comment":"Cover (back)"}},
              {"index":3,"codec_type":"video","disposition":{"attached_pic":1},"tags":{"comment":"Cover (front)"}}]}
            """);
        Assert.Equal("斜陽", metadata.Title);
        Assert.Equal("ヨルシカ", metadata.Artist);
        Assert.Equal("专辑名", metadata.Album);
        Assert.Equal("词曲作者", metadata.Author);
        Assert.Equal(TimeSpan.FromSeconds(234.56), metadata.Duration);
        Assert.Equal(3, metadata.CoverStream);
    }

    [Fact]
    public void AudioStreamFillsMissingFieldsButImageTagsAreNotSongTags()
    {
        var metadata = AudioMetadataReader.Parse("""
            {"format":{"duration":"N/A","tags":{"title":""}},"streams":[
              {"index":0,"codec_type":"audio","tags":{"title":"歌曲","album_artist":"专辑艺人","author":"作者"}},
              {"index":1,"codec_type":"video","disposition":{"attached_pic":1},"tags":{"title":"Album cover","album":"图片标签"}}]}
            """);
        Assert.Equal("歌曲", metadata.Title);
        Assert.Equal("专辑艺人", metadata.Artist);
        Assert.Equal("作者", metadata.Author);
        Assert.Null(metadata.Album);
        Assert.Equal(TimeSpan.Zero, metadata.Duration);
        Assert.Equal(1, metadata.CoverStream);
    }

    [Fact]
    public void UntaggedFilesKeepAllFallbackFieldsAvailable()
    {
        var metadata = AudioMetadataReader.Parse("""{"format":{},"streams":[{"index":0,"codec_type":"audio"}]}""");
        Assert.Null(metadata.Title);
        Assert.Null(metadata.Artist);
        Assert.Null(metadata.Album);
        Assert.Null(metadata.Author);
        Assert.Null(metadata.CoverStream);
    }
}
