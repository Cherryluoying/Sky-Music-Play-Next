// 模块：SkyMusic.Infrastructure 宏脚本领域 MacroKeyCatalog
namespace SkyMusic.Infrastructure.Automation;

public static class MacroKeyCatalog
{
    private static readonly HashSet<string> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Space", "Tab", "Enter", "Escape", "Backspace", "Delete", "Insert",
        "Home", "End", "PageUp", "PageDown", "Up", "Down", "Left", "Right",
        "LeftShift", "RightShift", "LeftCtrl", "RightCtrl", "LeftAlt", "RightAlt",
        "Comma", "Period", "Slash", "Semicolon", "Quote", "Minus", "Equals",
        "LeftBracket", "RightBracket", "Backslash", "Grave"
    };

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 1 && char.IsAsciiLetterOrDigit(normalized[0]))
        {
            normalized = normalized.ToUpperInvariant();
            return true;
        }

        if (normalized.Length is 2 or 3 && normalized[0] is 'F' or 'f' &&
            int.TryParse(normalized.AsSpan(1), out var functionKey) && functionKey is >= 1 and <= 12)
        {
            normalized = $"F{functionKey}";
            return true;
        }

        var candidate = normalized;
        var match = NamedKeys.FirstOrDefault(key => string.Equals(key, candidate, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            normalized = string.Empty;
            return false;
        }

        normalized = match;
        return true;
    }
}
