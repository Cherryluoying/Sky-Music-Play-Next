// 模块：SkyMusic.App 界面状态 InstrumentChoiceViewModel
using Avalonia.Media.Imaging;
using SkyMusic.App.Services;
using SkyMusic.Core.GameScores;

namespace SkyMusic.App.ViewModels;

public sealed class InstrumentChoiceViewModel(InstrumentAssetDefinition definition)
{
    public string Id => definition.Id;

    public string DisplayName => InstrumentDisplayNames.Get(definition.Id);

    public string Detail => $"{definition.NoteCount} 音色采样";

    public Bitmap? Artwork => ComposerArtworkCache.Get(definition.Profile);

    public override string ToString() => DisplayName;
}

internal static class InstrumentDisplayNames
{
    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["Lyre"] = "风物之诗琴",
        ["Vintage-Lyre"] = "老旧的诗琴",
        ["Zither"] = "镜花之琴",
        ["Old-Zither"] = "老旧的筝",
        ["LingeringEuphonia"] = "余音",
        ["LeapingSpiritPiano"] = "跃动旋律",
        ["HarmonicKey"] = "和声键盘",
        ["DjemDjemDrum"] = "俱利鼓",
        ["Piano"] = "钢琴",
        ["GrandPiano"] = "大钢琴",
        ["WinterPiano"] = "冬日钢琴",
        ["Harp"] = "竖琴",
        ["Guitar"] = "吉他",
        ["LightGuitar"] = "轻音吉他",
        ["ToyUkulele"] = "玩具尤克里里",
        ["Flute"] = "长笛",
        ["Panflute"] = "排箫",
        ["Ocarina"] = "陶笛",
        ["MantaOcarina"] = "遥鲲陶笛",
        ["Trumpet"] = "号角",
        ["Horn"] = "圆号",
        ["Xylophone"] = "木琴",
        ["Kalimba"] = "拇指琴",
        ["HandPan"] = "手碟",
        ["Drum"] = "鼓",
        ["DunDun"] = "咚咚鼓",
        ["Bells"] = "钟琴",
        ["Contrabass"] = "低音提琴",
        ["Pipa"] = "琵琶",
        ["Aurora"] = "极光之声"
    };

    public static string Get(string id) => Names.TryGetValue(id, out var name) ? name : id;
}
