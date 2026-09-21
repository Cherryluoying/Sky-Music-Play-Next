// 模块：SkyMusic.Backend.Tests 后端测试 MacroPlaybackSessionTests
using SkyMusic.Core.Automation;
using SkyMusic.Core.Services;
using SkyMusic.Infrastructure.Automation;

namespace SkyMusic.Backend.Tests;

public sealed class MacroPlaybackSessionTests
{
    [Fact]
    public async Task PlaysEventsAndReleasesKeys()
    {
        var sink = new RecordingMacroSink();
        await using var session = new MacroPlaybackSession(sink);
        var script = new MacroScript("test",
        [
            new MacroEvent("A", MacroKeyAction.Down, 0),
            new MacroEvent("A", MacroKeyAction.Up, 1_000)
        ]);

        await session.LoadAsync(script);
        await session.StartAsync();
        await WaitUntilAsync(() => session.Snapshot.State == SkyMusic.Core.Playback.AutoPlayState.Completed);

        Assert.Equal([MacroKeyAction.Down, MacroKeyAction.Up], sink.Events.Select(item => item.Action));
        Assert.True(sink.WasReset);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(5, timeout.Token);
        }
    }

    private sealed class RecordingMacroSink : IMacroInputSink
    {
        public List<MacroEvent> Events { get; } = [];

        public bool WasReset { get; private set; }

        public void Send(MacroEvent macroEvent) => Events.Add(macroEvent);

        public void Reset() => WasReset = true;
    }
}
