// 模块：键位选择与物理键录入；显示键帽符号，持久化仍使用 Windows Set 1 扫描码。
using Avalonia.Input;

namespace SkyMusic.App.Services;

public sealed record KeyboardKey(PhysicalKey PhysicalKey, string Label, int ScanCode, bool IsExtended = false);

public static class KeyboardKeyCatalog
{
    public static IReadOnlyList<KeyboardKey> Keys { get; } = CreateKeys();

    public static KeyboardKey FromScanCode(int code, bool extended) =>
        Keys.FirstOrDefault(key => key.ScanCode == code && key.IsExtended == extended)
        ?? new KeyboardKey(PhysicalKey.None, $"其他键（0x{code:X2}{(extended ? " · 扩展" : "")}）", code, extended);

    public static KeyboardKey? FromPhysicalKey(PhysicalKey key) => Keys.FirstOrDefault(item => item.PhysicalKey == key);

    // 按物理位置映射，避免中文输入法、Shift 和键盘布局把字符码误当成扫描码。
    private static IReadOnlyList<KeyboardKey> CreateKeys()
    {
        var keys = new List<KeyboardKey>();
        void Add(PhysicalKey key, string label, int code, bool extended = false) => keys.Add(new(key, label, code, extended));
        void Row(string letters, int firstCode)
        {
            for (var i = 0; i < letters.Length; i++) Add(Enum.Parse<PhysicalKey>(letters[i].ToString()), letters[i].ToString(), firstCode + i);
        }
        Row("QWERTYUIOP", 0x10);
        Row("ASDFGHJKL", 0x1E);
        Row("ZXCVBNM", 0x2C);
        for (var i = 1; i <= 9; i++) Add(Enum.Parse<PhysicalKey>($"Digit{i}"), i.ToString(), i + 1);
        Add(PhysicalKey.Digit0, "0", 0x0B);
        Add(PhysicalKey.Minus, "- / _", 0x0C);
        Add(PhysicalKey.Equal, "= / +", 0x0D);
        Add(PhysicalKey.BracketLeft, "[ / {", 0x1A);
        Add(PhysicalKey.BracketRight, "] / }", 0x1B);
        Add(PhysicalKey.Semicolon, "; / :", 0x27);
        Add(PhysicalKey.Quote, "' / \"", 0x28);
        Add(PhysicalKey.Backquote, "` / ~", 0x29);
        Add(PhysicalKey.Backslash, "\\ / |", 0x2B);
        Add(PhysicalKey.Comma, ", / <", 0x33);
        Add(PhysicalKey.Period, ". / >", 0x34);
        Add(PhysicalKey.Slash, "/ / ?", 0x35);
        Add(PhysicalKey.Space, "空格 Space", 0x39);
        Add(PhysicalKey.Enter, "回车 Enter", 0x1C);
        Add(PhysicalKey.Tab, "Tab", 0x0F);
        Add(PhysicalKey.Backspace, "退格 Backspace", 0x0E);
        Add(PhysicalKey.Escape, "Esc", 0x01);
        Add(PhysicalKey.ArrowUp, "↑", 0x48, true);
        Add(PhysicalKey.ArrowDown, "↓", 0x50, true);
        Add(PhysicalKey.ArrowLeft, "←", 0x4B, true);
        Add(PhysicalKey.ArrowRight, "→", 0x4D, true);
        Add(PhysicalKey.Home, "Home", 0x47, true);
        Add(PhysicalKey.End, "End", 0x4F, true);
        Add(PhysicalKey.PageUp, "Page Up", 0x49, true);
        Add(PhysicalKey.PageDown, "Page Down", 0x51, true);
        Add(PhysicalKey.Insert, "Insert", 0x52, true);
        Add(PhysicalKey.Delete, "Delete", 0x53, true);
        for (var i = 1; i <= 12; i++) Add(Enum.Parse<PhysicalKey>($"F{i}"), $"F{i}", i <= 10 ? 0x3A + i : 0x57 + i - 11);
        int[] numpadCodes = [0x52, 0x4F, 0x50, 0x51, 0x4B, 0x4C, 0x4D, 0x47, 0x48, 0x49];
        for (var i = 0; i <= 9; i++) Add(Enum.Parse<PhysicalKey>($"NumPad{i}"), $"数字键盘 {i}", numpadCodes[i]);
        Add(PhysicalKey.NumPadDecimal, "数字键盘 .", 0x53);
        Add(PhysicalKey.NumPadAdd, "数字键盘 +", 0x4E);
        Add(PhysicalKey.NumPadSubtract, "数字键盘 -", 0x4A);
        Add(PhysicalKey.NumPadMultiply, "数字键盘 *", 0x37);
        Add(PhysicalKey.NumPadDivide, "数字键盘 /", 0x35, true);
        Add(PhysicalKey.NumPadEnter, "数字键盘 Enter", 0x1C, true);
        return keys;
    }
}
