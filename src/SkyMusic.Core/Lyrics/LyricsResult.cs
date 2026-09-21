// 模块：SkyMusic.Core 歌词领域 LyricsResult
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Lyrics;

public sealed record LyricsResult(string Id, string Provider, IReadOnlyList<LyricLine> Lines);
