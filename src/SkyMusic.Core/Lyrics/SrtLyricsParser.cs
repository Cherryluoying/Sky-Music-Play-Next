// 模块：SRT 歌词导入；字幕序号和时间轴不进入正文，多行译文沿用现有双语歌词布局。
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Lyrics;

public static partial class SrtLyricsParser
{
    [GeneratedRegex(@"^(?<start>\d{2,3}:\d{2}:\d{2}[,.]\d{3})\s*-->\s*(?<end>\d{2,3}:\d{2}:\d{2}[,.]\d{3})(?:\s+.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TimeRange();
    [GeneratedRegex(@"</?(?:b|i|u|font)(?:\s+[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FormattingTags();

    // 转为现有歌词时间轴：使用字幕开始时间，直到下一句切换；非法时间块直接跳过。
    public static IReadOnlyList<LyricLine> Parse(string content)
    {
        var result = new List<LyricLine>();
        foreach (var block in Regex.Split(content.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n'), @"\n\s*\n"))
        {
            var lines = block.Split('\n').Select(line => line.Trim()).ToArray();
            var index = Array.FindIndex(lines, line => TimeRange().IsMatch(line));
            if (index < 0) continue;
            var match = TimeRange().Match(lines[index]);
            if (!TryTime(match.Groups["start"].Value, out var start) ||
                !TryTime(match.Groups["end"].Value, out var end) || end <= start) continue;
            var text = WebUtility.HtmlDecode(FormattingTags().Replace(string.Join('\n', lines.Skip(index + 1)), "")).Trim();
            if (text.Length > 0) result.Add(new LyricLine(start, text));
        }
        return LyricLines.Normalize(result);
    }

    private static bool TryTime(string value, out TimeSpan time)
    {
        time = default;
        var parts = value.Split(':', ',', '.');
        if (parts.Length != 4 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(parts[1], out var minutes) || minutes > 59 ||
            !int.TryParse(parts[2], out var seconds) || seconds > 59 || !int.TryParse(parts[3], out var milliseconds)) return false;
        time = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }
}
