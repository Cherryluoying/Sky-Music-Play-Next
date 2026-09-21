// 模块：SkyMusic.Infrastructure 输入设备 WindowsScanCodeSink
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Input;

public sealed class WindowsScanCodeSink : IPlaybackEventSink
{
    private readonly object _sync = new();
    private readonly KeyboardMappingProfile _profile;
    private readonly Dictionary<ScanCodeBinding, int> _pressed = new();

    public WindowsScanCodeSink(KeyboardMappingProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    public string Name => $"Windows SendInput ({_profile.Name})";

    public PlaybackSinkCapabilities Capabilities =>
        PlaybackSinkCapabilities.ForegroundInput |
        PlaybackSinkCapabilities.ScanCodes |
        PlaybackSinkCapabilities.SimultaneousKeys;

    public void Send(PlaybackEvent playbackEvent)
    {
        if (!_profile.TryGetBinding(playbackEvent.MidiNote, out var binding))
        {
            return;
        }

        lock (_sync)
        {
            if (playbackEvent.Type == PlaybackEventType.KeyDown)
            {
                Press(binding);
            }
            else
            {
                Release(binding);
            }
        }
    }

    public void Reset()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        lock (_sync)
        {
            Exception? failure = null;
            foreach (var binding in _pressed.Keys.ToArray())
            {
                try
                {
                    WindowsScanCodeEmitter.Send(binding, true);
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }
            }

            _pressed.Clear();
            if (failure is not null)
            {
                throw failure;
            }
        }
    }

    private void Press(ScanCodeBinding binding)
    {
        if (_pressed.TryGetValue(binding, out var count))
        {
            _pressed[binding] = count + 1;
            return;
        }

        WindowsScanCodeEmitter.Send(binding, false);
        _pressed[binding] = 1;
    }

    private void Release(ScanCodeBinding binding)
    {
        if (!_pressed.TryGetValue(binding, out var count))
        {
            return;
        }

        if (count > 1)
        {
            _pressed[binding] = count - 1;
            return;
        }

        WindowsScanCodeEmitter.Send(binding, true);
        _pressed.Remove(binding);
    }
}
