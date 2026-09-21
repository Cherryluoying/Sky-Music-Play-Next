// 模块：SkyMusic.Core 歌词领域 LyricsQuery
namespace SkyMusic.Core.Lyrics;

public sealed record LyricsQuery(string Title, string Artist, string Album, TimeSpan Duration);
