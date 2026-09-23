// 模块：SkyMusic.Core 本地 LRC 与纯文本歌词解析
using System.Globalization;
using System.Text.RegularExpressions;
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Lyrics;

public static partial class LrcLyricsParser
{
    // 支持常见的 [mm:ss.xx]、[mm:ss:xxx] 以及一行多个时间标签。
    [GeneratedRegex(@"\[(?<minute>\d{1,3}):(?<second>\d{1,2})(?:[\.:](?<fraction>\d{1,3}))?\]", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();

    public static async ValueTask<IReadOnlyList<LyricLine>> ParseFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(Path.GetFullPath(path), cancellationToken);
        return Parse(content);
    }

    public static IReadOnlyList<LyricLine> Parse(string content)
    {
        var timedLines = new List<LyricLine>();
        var plainLines = new List<string>();
        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var matches = TimestampRegex().Matches(line);
            if (matches.Count == 0)
            {
                // LRC 元数据不是歌词，纯文本文件则作为无时间歌词处理。
                if (!Regex.IsMatch(line, @"^\[[a-zA-Z]+:.*\]$"))
                {
                    plainLines.Add(line);
                }
                continue;
            }

            var text = TimestampRegex().Replace(line, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
                var seconds = int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture);
                var fractionText = match.Groups["fraction"].Value;
                var milliseconds = fractionText.Length switch
                {
                    1 => int.Parse(fractionText, CultureInfo.InvariantCulture) * 100,
                    2 => int.Parse(fractionText, CultureInfo.InvariantCulture) * 10,
                    3 => int.Parse(fractionText, CultureInfo.InvariantCulture),
                    _ => 0
                };
                timedLines.Add(new LyricLine(
                    TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(milliseconds),
                    text));
            }
        }

        if (timedLines.Count > 0)
        {
            return timedLines.OrderBy(line => line.Timestamp).ToArray();
        }

        // 普通 TXT 没有时间信息时提供可用的默认时间轴，用户仍可点击定位。
        return plainLines
            .Select((text, index) => new LyricLine(TimeSpan.FromSeconds(index * 5), text))
            .ToArray();
    }
}
