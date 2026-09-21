// 模块：SkyMusic.Infrastructure 音乐目录 DemoMusicCatalog
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Catalog;

public sealed class DemoMusicCatalog : IMusicCatalog
{
    private static readonly IReadOnlyList<MusicTrack> Tracks =
    [
        CreateTrack(
            "cloud-field",
            "起风了",
            "买辣椒也用券",
            "翻唱精选",
            "avares://SkyMusic.App/Assets/cover-cloud.jpg",
            319,
            [
                (0, "这一路上走走停停"),
                (18, "顺着少年漂流的痕迹"),
                (39, "迈出车站的前一刻"),
                (63, "竟有些犹豫"),
                (88, "不禁笑这近乡情怯"),
                (116, "仍无可避免"),
                (147, "而长野的天"),
                (181, "依旧那么暖"),
                (218, "风吹起了从前"),
                (257, "从前初识这世间")
            ]),
        CreateTrack(
            "blue-hour",
            "蓝色时刻",
            "WindHide",
            "夜航记录",
            "avares://SkyMusic.App/Assets/cover-blue.jpg",
            286,
            [
                (0, "城市熄灭最后一盏灯"),
                (27, "月色落在没有名字的河"),
                (55, "我们交换沉默和远方"),
                (91, "让节拍替夜晚回答"),
                (131, "蓝色时刻不必匆忙"),
                (176, "下一站仍有风景亮起"),
                (226, "把未完成的歌留给黎明")
            ]),
        CreateTrack(
            "flower-letter",
            "写给春天",
            "April Notes",
            "花与信",
            "avares://SkyMusic.App/Assets/cover-flower.jpg",
            244,
            [
                (0, "把清晨折进一封短信"),
                (31, "寄往花朵醒来的地方"),
                (66, "雨水写下透明的句点"),
                (104, "而你读懂所有留白"),
                (151, "等风吹开新的篇章"),
                (201, "我们会在春天重逢")
            ]),
        CreateTrack(
            "quiet-station",
            "静默车站",
            "Northbound",
            "凌晨列车",
            "avares://SkyMusic.App/Assets/cover-night.jpg",
            352,
            [
                (0, "站台保存着昨夜的雨"),
                (42, "远处列车穿过薄雾"),
                (86, "没有告别也没有迟疑"),
                (132, "只把名字写进旅途"),
                (191, "时间沿着铁轨延伸"),
                (249, "回忆在下一站停靠"),
                (309, "天亮以前继续前行")
            ]),
        CreateTrack(
            "piano-room",
            "练习室灯光",
            "Piano Room",
            "第八十八键",
            "avares://SkyMusic.App/Assets/cover-piano.jpg",
            273,
            [
                (0, "第一遍还听得见犹豫"),
                (34, "第二遍让节拍慢慢靠近"),
                (73, "黑白之间没有捷径"),
                (117, "只有指尖记住的距离"),
                (164, "当灯光照过第八十八键"),
                (218, "旋律终于成为自己")
            ])
    ];

    public Task<IReadOnlyList<MusicTrack>> GetFeaturedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Tracks);

    private static MusicTrack CreateTrack(
        string id,
        string title,
        string artist,
        string album,
        string cover,
        int durationSeconds,
        IReadOnlyList<(int Seconds, string Text)> lyrics)
        => new(
            id,
            title,
            artist,
            album,
            cover,
            TimeSpan.FromSeconds(durationSeconds),
            lyrics.Select(line => new LyricLine(TimeSpan.FromSeconds(line.Seconds), line.Text)).ToArray());
}
