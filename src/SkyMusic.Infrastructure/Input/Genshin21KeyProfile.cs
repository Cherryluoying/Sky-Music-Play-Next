// 模块：SkyMusic.Infrastructure 输入设备 Genshin21KeyProfile
namespace SkyMusic.Infrastructure.Input;

public static class Genshin21KeyProfile
{
    public static KeyboardMappingProfile Create() => new(
        "Genshin 21-key",
        new Dictionary<int, ScanCodeBinding>
        {
            [48] = new(0x2C),
            [50] = new(0x2D),
            [52] = new(0x2E),
            [53] = new(0x2F),
            [55] = new(0x30),
            [57] = new(0x31),
            [59] = new(0x32),
            [60] = new(0x1E),
            [62] = new(0x1F),
            [64] = new(0x20),
            [65] = new(0x21),
            [67] = new(0x22),
            [69] = new(0x23),
            [71] = new(0x24),
            [72] = new(0x10),
            [74] = new(0x11),
            [76] = new(0x12),
            [77] = new(0x13),
            [79] = new(0x14),
            [81] = new(0x15),
            [83] = new(0x16)
        });
}
