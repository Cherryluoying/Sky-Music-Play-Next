// 模块：SkyMusic.Backend.Tests 后端测试 JsonMacroScriptImporterTests
using System.Text;
using SkyMusic.Core.Automation;
using SkyMusic.Infrastructure.Automation;

namespace SkyMusic.Backend.Tests;

public sealed class JsonMacroScriptImporterTests
{
    [Fact]
    public async Task ImportsLegacyRelativeDelayFormat()
    {
        const string json = """
            [
              { "key": "2", "type": "Down", "delay": "124" },
              { "key": "2", "type": "Up", "delay": "1371" },
              { "key": "Space", "type": "Down", "delay": 77 }
            ]
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await new JsonMacroScriptImporter().ImportAsync(stream, "route.txt.json");

        Assert.True(result.IsSuccess);
        Assert.Equal([124_000L, 1_495_000L, 1_572_000L], result.Script!.Events.Select(item => item.TimeMicroseconds));
        Assert.Equal(MacroKeyAction.Up, result.Script.Events[1].Action);
    }

    [Fact]
    public async Task RejectsFieldShiftAndUnknownKeys()
    {
        const string json = """
            [
              { "key": "W", "type": "Down", "delay": "KeyUp:A" },
              { "key": "LaunchProgram", "type": "Up", "delay": "10" }
            ]
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await new JsonMacroScriptImporter().ImportAsync(stream, "broken.json");

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Errors.Count);
    }
}
