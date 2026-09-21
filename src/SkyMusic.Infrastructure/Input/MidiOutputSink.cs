// 模块：SkyMusic.Infrastructure 输入设备 MidiOutputSink
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Input;

public sealed class MidiOutputSink : IPlaybackEventSink, IDisposable
{
    private readonly object _sync = new();
    private IntPtr _output;
    private bool _disposed;

    public MidiOutputSink(int deviceIndex)
    {
        RtMidiNative.EnsureCompatible();
        Name = $"MIDI ({RtMidiNative.GetOutputPortName(deviceIndex)})";
        _output = RtMidiNative.skymusic_midi_output_create();
        if (_output == IntPtr.Zero || RtMidiNative.skymusic_midi_output_open(_output, (uint)deviceIndex) != 0)
        {
            if (_output != IntPtr.Zero)
                RtMidiNative.skymusic_midi_output_destroy(_output);
            _output = IntPtr.Zero;
            throw new IOException("MIDI output device could not be opened");
        }
    }

    public string Name { get; }

    public PlaybackSinkCapabilities Capabilities =>
        PlaybackSinkCapabilities.MidiOutput |
        PlaybackSinkCapabilities.SimultaneousKeys;

    public void Send(PlaybackEvent playbackEvent)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var channel = (byte)Math.Clamp(playbackEvent.Channel, 0, 15);
        var message = new[]
        {
            (byte)((playbackEvent.Type == PlaybackEventType.KeyDown ? 0x90 : 0x80) | channel),
            (byte)playbackEvent.MidiNote,
            playbackEvent.Type == PlaybackEventType.KeyDown ? playbackEvent.Velocity : (byte)0
        };
        lock (_sync)
        {
            if (RtMidiNative.skymusic_midi_output_send(_output, message, (uint)message.Length) != 0)
                throw new IOException("MIDI message could not be sent");
        }
    }

    public void Reset()
    {
        if (_disposed || _output == IntPtr.Zero)
            return;
        lock (_sync)
        {
            for (byte channel = 0; channel < 16; channel++)
            {
                var message = new[] { (byte)(0xB0 | channel), (byte)123, (byte)0 };
                RtMidiNative.skymusic_midi_output_send(_output, message, (uint)message.Length);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Reset();
        _disposed = true;
        if (_output != IntPtr.Zero)
        {
            RtMidiNative.skymusic_midi_output_destroy(_output);
            _output = IntPtr.Zero;
        }
    }
}
