// 模块：SkyMusic.Core 界面模型 MusicTrack
namespace SkyMusic.Core.Models;

public sealed record MusicTrack(
    string Id,
    string Title,
    string Artist,
    string Album,
    string CoverSource,
    TimeSpan Duration,
    IReadOnlyList<LyricLine> Lyrics,
    MediaKind Kind = MediaKind.Audio,
    string? SourcePath = null,
    string Author = "",
    string? LyricsSourcePath = null);
