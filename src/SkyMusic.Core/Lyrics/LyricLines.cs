// 模块：统一双语歌词；兼容 music_web_gateway 的句尾中文括号与 [tr] 标记。
using SkyMusic.Core.Models;
using System.Text.RegularExpressions;
namespace SkyMusic.Core.Lyrics;

public static partial class LyricLines
{
    // 与网页端 parseLyrics 相同：仅识别句尾含中文的括号，不拆分英文括号或普通标点。
    [GeneratedRegex(@"^(.*?)\s*[（(]([^()（）]*[\u3400-\u9fff][^()（）]*)[）)]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex InlineTranslationRegex();

    [GeneratedRegex(@"\[tr\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TranslationMarkerRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    // 本地导入、已有曲库与云端缓存统一经过这里；重复规范化不会丢失或重复翻译。
    public static IReadOnlyList<LyricLine> Normalize(IEnumerable<LyricLine> lines)
    {
        var source = lines.ToArray();
        var splitUnmarked = LooksLikeUnmarkedBilingual(source);
        return source
        .OrderBy(line => line.Timestamp).GroupBy(line => line.Timestamp)
        .Select(group =>
        {
            var originals = new List<string>();
            var translations = new List<string>();
            foreach (var line in group)
            {
                var parts = SplitLines(line.Text);
                if (parts.Length > 0)
                {
                    var first = parts[0];
                    if (TranslationMarkerRegex().IsMatch(first))
                        translations.Add(CleanTranslation(first));
                    else
                    {
                        var match = InlineTranslationRegex().Match(first);
                        // 只有括号、没有原文时保留整句，避免生成一个空原文行。
                        if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                        {
                            originals.Add(match.Groups[1].Value.Trim());
                            translations.Add(match.Groups[2].Value.Trim());
                        }
                        else if (splitUnmarked && string.IsNullOrWhiteSpace(line.Translation) && parts.Length == 1 &&
                                 TrySplitUnmarked(first, out var original, out var translation))
                        {
                            originals.Add(original);
                            translations.Add(translation);
                        }
                        else originals.Add(first);
                    }
                    translations.AddRange(parts.Skip(1).Select(CleanTranslation));
                }
                translations.AddRange(SplitLines(line.Translation).Select(CleanTranslation));
            }

            // [tr] 行即使出现在原文之前，也按时间戳归属原文；同时间戳的普通双行仍兼容。
            var uniqueOriginals = originals.Distinct(StringComparer.Ordinal).ToArray();
            var text = uniqueOriginals.FirstOrDefault() ?? translations.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "♪";
            var secondary = uniqueOriginals.Skip(1).Concat(translations)
                .Where(value => !string.IsNullOrWhiteSpace(value) && value != text).Distinct(StringComparer.Ordinal).ToArray();
            return new LyricLine(group.Key, text, secondary.Length > 0 ? string.Join("\n", secondary) : null);
        }).ToArray();
    }

    // 空格不是标准的双语分隔符。只有整份歌词反复呈现“含假名原文 + 无假名中文”
    // 且至少两句有中文用语线索时才启用推断，单句、全汉字歌词和普通日文空格保持原样。
    private static bool LooksLikeUnmarkedBilingual(IReadOnlyList<LyricLine> lines)
    {
        var japanese = 0;
        var candidates = 0;
        var chineseHints = 0;
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line.Translation) || SplitLines(line.Text).Length != 1 ||
                TranslationMarkerRegex().IsMatch(line.Text) || InlineTranslationRegex().IsMatch(line.Text)) continue;
            if (!line.Text.Any(IsKana)) continue;
            japanese++;
            if (!TrySplitUnmarked(line.Text, out _, out var translated)) continue;
            candidates++;
            // 文档级线索只决定是否启用；同文档中“此刻不知干渴”等句子无需逐句命中。
            if (translated.IndexOfAny("这们吗么着过还让请将却从为与丽缓飞见无来时会说听爱梦颗愿".ToCharArray()) >= 0 ||
                translated.Contains('的') || translated.Contains('了')) chineseHints++;
        }
        return candidates >= 3 && candidates * 5 >= japanese * 3 && chineseHints >= 2;
    }

    // 寻找最后一段假名之后的空格边界；中文译文自身的空格全部保留。
    private static bool TrySplitUnmarked(string text, out string original, out string translation)
    {
        foreach (Match gap in WhitespaceRegex().Matches(text))
        {
            var left = text[..gap.Index].Trim();
            var right = text[(gap.Index + gap.Length)..].Trim();
            if (!left.Any(IsKana) || right.Any(IsKana) || right.Count(IsHan) < 2) continue;
            original = left;
            translation = right;
            return true;
        }
        original = text;
        translation = string.Empty;
        return false;
    }

    private static bool IsKana(char value) => value is >= '\u3041' and <= '\u3096' or >= '\u30A1' and <= '\u30FA' or >= '\uFF66' and <= '\uFF9D';
    private static bool IsHan(char value) => value is >= '\u3400' and <= '\u9FFF';

    private static string[] SplitLines(string? text) => (text ?? string.Empty)
        .Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string CleanTranslation(string text) => TranslationMarkerRegex().Replace(text, string.Empty).Trim();
}
