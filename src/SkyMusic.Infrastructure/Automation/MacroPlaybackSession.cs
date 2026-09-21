// 模块：SkyMusic.Infrastructure 宏脚本领域 MacroPlaybackSession
using System.Collections.Immutable;
using SkyMusic.Core.Automation;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;
using SkyMusic.Infrastructure.Playback;

namespace SkyMusic.Infrastructure.Automation;

public sealed class MacroPlaybackSession : IMacroPlaybackSession
{
    private readonly MacroPlaybackEventSink _eventSink;
    private readonly AutoPlaySession _session;

    public MacroPlaybackSession(IMacroInputSink sink, ITimelineScheduler? scheduler = null)
    {
        _eventSink = new MacroPlaybackEventSink(sink ?? throw new ArgumentNullException(nameof(sink)));
        _session = new AutoPlaySession(scheduler ?? new HighPrecisionTimelineScheduler(), _eventSink);
    }

    public AutoPlaySnapshot Snapshot => _session.Snapshot;

    public event Action<AutoPlaySnapshot>? Changed
    {
        add => _session.Changed += value;
        remove => _session.Changed -= value;
    }

    public async ValueTask LoadAsync(MacroScript script, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);
        var keys = script.Events
            .Select(item => item.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((key, index) => (key, id: index))
            .ToDictionary(item => item.key, item => item.id, StringComparer.OrdinalIgnoreCase);
        _eventSink.SetKeys(keys.ToDictionary(item => item.Value, item => item.Key));

        // 宏键名在会话边界映射为稳定编号以复用高精度调度器
        var events = script.Events
            .Select(item => new PlaybackEvent(
                item.TimeMicroseconds,
                keys[item.Key],
                item.Action == MacroKeyAction.Down ? PlaybackEventType.KeyDown : PlaybackEventType.KeyUp,
                100,
                0,
                0))
            .ToImmutableArray();
        await _session.LoadAsync(new CompiledTimeline(events, script.DurationMicroseconds), cancellationToken);
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default) =>
        _session.StartAsync(cancellationToken);

    public ValueTask PauseAsync(CancellationToken cancellationToken = default) =>
        _session.PauseAsync(cancellationToken);

    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        _session.StopAsync(cancellationToken);

    public ValueTask SeekAsync(long positionMicroseconds, CancellationToken cancellationToken = default) =>
        _session.SeekAsync(positionMicroseconds, cancellationToken);

    public ValueTask SetSpeedAsync(double speed, CancellationToken cancellationToken = default) =>
        _session.SetSpeedAsync(speed, cancellationToken);

    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private sealed class MacroPlaybackEventSink(IMacroInputSink sink) : IPlaybackEventSink
    {
        private IReadOnlyDictionary<int, string> _keys = new Dictionary<int, string>();

        public string Name => "Keyboard macro";

        public PlaybackSinkCapabilities Capabilities =>
            PlaybackSinkCapabilities.ForegroundInput |
            PlaybackSinkCapabilities.ScanCodes |
            PlaybackSinkCapabilities.SimultaneousKeys;

        public void SetKeys(IReadOnlyDictionary<int, string> keys) => _keys = keys;

        public void Send(PlaybackEvent playbackEvent)
        {
            if (!_keys.TryGetValue(playbackEvent.MidiNote, out var key))
            {
                throw new InvalidOperationException("Macro timeline contains an unknown key id");
            }

            sink.Send(new MacroEvent(
                key,
                playbackEvent.Type == PlaybackEventType.KeyDown ? MacroKeyAction.Down : MacroKeyAction.Up,
                playbackEvent.TimeMicroseconds));
        }

        public void Reset() => sink.Reset();
    }
}
