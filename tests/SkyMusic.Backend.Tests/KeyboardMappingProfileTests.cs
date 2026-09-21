// 模块：SkyMusic.Backend.Tests 后端测试 KeyboardMappingProfileTests
using SkyMusic.Infrastructure.Input;

namespace SkyMusic.Backend.Tests;

public sealed class KeyboardMappingProfileTests
{
    [Fact]
    public void LegacyProfilePreservesOriginalFifteenKeyLayout()
    {
        var profile = LegacySky15KeyProfile.Create();

        Assert.Equal(15, profile.Bindings.Count);
        Assert.Equal(new ScanCodeBinding(0x15), profile.Bindings[60]);
        Assert.Equal(new ScanCodeBinding(0x25), profile.Bindings[72]);
        Assert.Equal(new ScanCodeBinding(0x35), profile.Bindings[84]);
    }

    [Fact]
    public void RejectsMappingsOutsidePianoRange()
    {
        var bindings = new Dictionary<int, ScanCodeBinding>
        {
            [20] = new(0x15)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardMappingProfile("Invalid", bindings));
    }

    [Fact]
    public void GenshinProfileMapsThreeDiatonicOctaves()
    {
        var profile = Genshin21KeyProfile.Create();

        Assert.Equal(21, profile.Bindings.Count);
        Assert.Equal(new ScanCodeBinding(0x2C), profile.Bindings[48]);
        Assert.Equal(new ScanCodeBinding(0x1E), profile.Bindings[60]);
        Assert.Equal(new ScanCodeBinding(0x10), profile.Bindings[72]);
        Assert.Equal(new ScanCodeBinding(0x16), profile.Bindings[83]);
    }
}
