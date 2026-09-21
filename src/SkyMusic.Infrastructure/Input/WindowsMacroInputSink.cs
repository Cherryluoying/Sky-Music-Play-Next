// 模块：SkyMusic.Infrastructure 输入设备 WindowsMacroInputSink
using SkyMusic.Core.Automation;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Input;

public sealed class WindowsMacroInputSink : IMacroInputSink
{
    private static readonly IReadOnlyDictionary<string, ScanCodeBinding> Bindings = CreateBindings();
    private readonly object _sync = new();
    private readonly HashSet<ScanCodeBinding> _pressed = [];

    // 将受信任宏事件转换为 Windows 扫描码输入
    public void Send(MacroEvent macroEvent)
    {
        if (!Bindings.TryGetValue(macroEvent.Key, out var binding))
        {
            throw new InvalidOperationException($"Unsupported macro key: {macroEvent.Key}");
        }

        lock (_sync)
        {
            var isKeyUp = macroEvent.Action == MacroKeyAction.Up;
            WindowsScanCodeEmitter.Send(binding, isKeyUp);
            if (isKeyUp)
            {
                _pressed.Remove(binding);
            }
            else
            {
                _pressed.Add(binding);
            }
        }
    }

    // 释放会话中仍处于按下状态的全部按键
    public void Reset()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        lock (_sync)
        {
            foreach (var binding in _pressed)
            {
                WindowsScanCodeEmitter.Send(binding, true);
            }

            _pressed.Clear();
        }
    }

    private static IReadOnlyDictionary<string, ScanCodeBinding> CreateBindings()
    {
        var bindings = new Dictionary<string, ScanCodeBinding>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = new(0x02),
            ["2"] = new(0x03),
            ["3"] = new(0x04),
            ["4"] = new(0x05),
            ["5"] = new(0x06),
            ["6"] = new(0x07),
            ["7"] = new(0x08),
            ["8"] = new(0x09),
            ["9"] = new(0x0A),
            ["0"] = new(0x0B),
            ["Minus"] = new(0x0C),
            ["Equals"] = new(0x0D),
            ["Backspace"] = new(0x0E),
            ["Tab"] = new(0x0F),
            ["Q"] = new(0x10),
            ["W"] = new(0x11),
            ["E"] = new(0x12),
            ["R"] = new(0x13),
            ["T"] = new(0x14),
            ["Y"] = new(0x15),
            ["U"] = new(0x16),
            ["I"] = new(0x17),
            ["O"] = new(0x18),
            ["P"] = new(0x19),
            ["LeftBracket"] = new(0x1A),
            ["RightBracket"] = new(0x1B),
            ["Enter"] = new(0x1C),
            ["LeftCtrl"] = new(0x1D),
            ["A"] = new(0x1E),
            ["S"] = new(0x1F),
            ["D"] = new(0x20),
            ["F"] = new(0x21),
            ["G"] = new(0x22),
            ["H"] = new(0x23),
            ["J"] = new(0x24),
            ["K"] = new(0x25),
            ["L"] = new(0x26),
            ["Semicolon"] = new(0x27),
            ["Quote"] = new(0x28),
            ["Grave"] = new(0x29),
            ["LeftShift"] = new(0x2A),
            ["Backslash"] = new(0x2B),
            ["Z"] = new(0x2C),
            ["X"] = new(0x2D),
            ["C"] = new(0x2E),
            ["V"] = new(0x2F),
            ["B"] = new(0x30),
            ["N"] = new(0x31),
            ["M"] = new(0x32),
            ["Comma"] = new(0x33),
            ["Period"] = new(0x34),
            ["Slash"] = new(0x35),
            ["RightShift"] = new(0x36),
            ["LeftAlt"] = new(0x38),
            ["Space"] = new(0x39),
            ["F1"] = new(0x3B),
            ["F2"] = new(0x3C),
            ["F3"] = new(0x3D),
            ["F4"] = new(0x3E),
            ["F5"] = new(0x3F),
            ["F6"] = new(0x40),
            ["F7"] = new(0x41),
            ["F8"] = new(0x42),
            ["F9"] = new(0x43),
            ["F10"] = new(0x44),
            ["F11"] = new(0x57),
            ["F12"] = new(0x58),
            ["Escape"] = new(0x01),
            ["RightCtrl"] = new(0x1D, true),
            ["RightAlt"] = new(0x38, true),
            ["Home"] = new(0x47, true),
            ["Up"] = new(0x48, true),
            ["PageUp"] = new(0x49, true),
            ["Left"] = new(0x4B, true),
            ["Right"] = new(0x4D, true),
            ["End"] = new(0x4F, true),
            ["Down"] = new(0x50, true),
            ["PageDown"] = new(0x51, true),
            ["Insert"] = new(0x52, true),
            ["Delete"] = new(0x53, true)
        };
        return bindings;
    }
}
