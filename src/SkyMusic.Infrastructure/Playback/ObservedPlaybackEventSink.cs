// 模块：SkyMusic.Infrastructure 播放领域 ObservedPlaybackEventSink
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Playback;

internal sealed class ObservedPlaybackEventSink(
    IPlaybackEventSink inner,
    PlaybackEventMonitor monitor) : IPlaybackEventSink, IDisposable
{
    private readonly HashSet<(int Note, int Track, int Channel)> _active = [];

    public string Name => inner.Name;

    public PlaybackSinkCapabilities Capabilities => inner.Capabilities;

    public void Send(PlaybackEvent playbackEvent)
    {
        inner.Send(playbackEvent);
        var key = (playbackEvent.MidiNote, playbackEvent.Track, playbackEvent.Channel);
        if (playbackEvent.Type == PlaybackEventType.KeyDown)
        {
            _active.Add(key);
        }
        else
        {
            _active.Remove(key);
        }
        monitor.Publish(playbackEvent);
    }

    public void Reset()
    {
        inner.Reset();
        foreach (var key in _active)
        {
            monitor.Publish(new PlaybackEvent(
                0,
                key.Note,
                PlaybackEventType.KeyUp,
                0,
                key.Track,
                key.Channel));
        }
        _active.Clear();
    }

    public void Dispose() => (inner as IDisposable)?.Dispose();
}
