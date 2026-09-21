// 模块：SkyMusic.Infrastructure 输入设备 LegacySky15KeyProfile
namespace SkyMusic.Infrastructure.Input;

public static class LegacySky15KeyProfile
{
    public static KeyboardMappingProfile Create() => new(
        "Sky legacy 15-key",
        new Dictionary<int, ScanCodeBinding>
        {
            [60] = new(0x15),
            [62] = new(0x16),
            [64] = new(0x17),
            [65] = new(0x18),
            [67] = new(0x19),
            [69] = new(0x23),
            [71] = new(0x24),
            [72] = new(0x25),
            [74] = new(0x26),
            [76] = new(0x27),
            [77] = new(0x31),
            [79] = new(0x32),
            [81] = new(0x33),
            [83] = new(0x34),
            [84] = new(0x35)
        });
}
