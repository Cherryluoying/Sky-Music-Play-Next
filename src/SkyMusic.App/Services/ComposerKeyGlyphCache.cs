// 模块：SkyMusic.App 桌面服务 ComposerKeyGlyphCache
using System.Globalization;
using System.Xml.Linq;
using Avalonia.Media;
using Avalonia.Platform;
using SkyMusic.Core.GameScores;

namespace SkyMusic.App.Services;

public static class ComposerKeyGlyphCache
{
    private static readonly string[] SkyGlyphs =
    [
        "dmcr", "dm", "cr", "dm", "cr",
        "cr", "dm", "dmcr", "dm", "cr",
        "cr", "dm", "cr", "dm", "dmcr"
    ];

    private static readonly string[] GenshinGlyphs = ["do", "re", "mi", "fa", "so", "la", "ti"];
    private static readonly Dictionary<string, Geometry?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static Geometry? Get(GameScoreProfile profile, int index)
    {
        var name = profile switch
        {
            GameScoreProfile.Sky when index >= 0 && index < SkyGlyphs.Length => SkyGlyphs[index],
            GameScoreProfile.Genshin when index >= 0 => GenshinGlyphs[index % GenshinGlyphs.Length],
            _ => null
        };
        if (name is null)
            return null;

        lock (Gate)
        {
            if (!Cache.TryGetValue(name, out var geometry))
            {
                geometry = Load(name);
                Cache[name] = geometry;
            }
            return geometry;
        }
    }

    private static Geometry? Load(string name)
    {
        try
        {
            var uri = new Uri($"avares://SkyMusic.App/Assets/GenshinMusic/Images/Keys/{name}.svg");
            using var stream = AssetLoader.Open(uri);
            var document = XDocument.Load(stream);
            var root = document.Root;
            if (root is null)
                return null;

            var segments = root.Descendants()
                .Select(ToPathData)
                .Where(value => !string.IsNullOrWhiteSpace(value));
            var pathData = string.Join(' ', segments);
            return string.IsNullOrWhiteSpace(pathData) ? null : Geometry.Parse(pathData);
        }
        catch (Exception)
        {
            // 素材损坏时保留按键文字
            return null;
        }
    }

    private static string? ToPathData(XElement element)
    {
        return element.Name.LocalName switch
        {
            "path" => element.Attribute("d")?.Value,
            "circle" => CirclePath(element),
            "rect" => DiamondPath(element),
            _ => null
        };
    }

    private static string? CirclePath(XElement element)
    {
        if (!Number(element, "cx", out var cx) || !Number(element, "cy", out var cy) ||
            !Number(element, "r", out var radius))
            return null;
        cx -= 52.444184;
        cy -= 69.21989;
        return FormattableString.Invariant(
            $"M {cx - radius},{cy} A {radius},{radius} 0 1 0 {cx + radius},{cy} A {radius},{radius} 0 1 0 {cx - radius},{cy} Z");
    }

    private static string? DiamondPath(XElement element)
    {
        if (!Number(element, "width", out var width))
            return null;

        // 原素材矩形旋转 45 度后的中心与圆形一致
        const double cx = 61.679906;
        const double cy = 61.60854;
        var radius = width / Math.Sqrt(2);
        return FormattableString.Invariant(
            $"M {cx},{cy - radius} L {cx + radius},{cy} L {cx},{cy + radius} L {cx - radius},{cy} Z");
    }

    private static bool Number(XElement element, string name, out double value)
        => double.TryParse(
            element.Attribute(name)?.Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
}
