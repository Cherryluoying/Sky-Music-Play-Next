// 模块：SkyMusic.Infrastructure 输入设备 MidiInputCapture
using System.Diagnostics;
using System.Runtime.InteropServices;
using SkyMusic.Core.Midi;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Input;

public sealed class MidiInputCapture : IMidiInputCapture
{
    private readonly object _gate = new();
    private readonly Stopwatch _clock = new();
    private readonly RtMidiNative.MessageCallback _callback;
    private IntPtr _input;
    private IReadOnlyList<MidiInputDeviceInfo> _devices = [];

    public MidiInputCapture()
    {
        _callback = OnMessage;
        if (!OperatingSystem.IsWindows())
            return;

        RtMidiNative.EnsureCompatible();
        _input = RtMidiNative.skymusic_midi_input_create(_callback, IntPtr.Zero);
        if (_input == IntPtr.Zero)
            throw new IOException("RtMidi input could not be created");
        RefreshDevices();
    }

    public IReadOnlyList<MidiInputDeviceInfo> Devices => _devices;

    public MidiInputDeviceInfo? CurrentDevice { get; private set; }

    public bool IsListening => CurrentDevice is not null;

    public event Action<MidiNoteMessage>? NoteChanged;

    // 从 RtMidi 刷新当前可用输入设备
    public IReadOnlyList<MidiInputDeviceInfo> RefreshDevices()
    {
        if (_input == IntPtr.Zero)
            return _devices = [];

        var count = RtMidiNative.skymusic_midi_input_port_count();
        if (count < 0)
            throw new IOException("RtMidi input devices could not be enumerated");
        _devices = Enumerable.Range(0, count)
            .Select(index => new MidiInputDeviceInfo(index, RtMidiNative.GetInputPortName(index)))
            .ToArray();
        return _devices;
    }

    // 打开设备并注册低开销原生回调
    public void Start(int deviceIndex)
    {
        lock (_gate)
        {
            StopCore();
            var info = _devices.FirstOrDefault(device => device.Index == deviceIndex)
                ?? throw new ArgumentOutOfRangeException(nameof(deviceIndex), "MIDI input device was not found");
            if (RtMidiNative.skymusic_midi_input_open(_input, (uint)deviceIndex) != 0)
                throw new IOException("MIDI input device could not be opened");
            _clock.Restart();
            CurrentDevice = info;
        }
    }

    public void Stop()
    {
        lock (_gate)
            StopCore();
    }

    // 将原生 MIDI 字节转换为托管音符消息
    private void OnMessage(IntPtr userData, double deltaSeconds, IntPtr data, uint size)
    {
        if (size < 3 || data == IntPtr.Zero)
            return;
        var status = Marshal.ReadByte(data);
        var type = status & 0xF0;
        if (type is not (0x80 or 0x90))
            return;

        var note = Marshal.ReadByte(data, 1);
        var velocity = Marshal.ReadByte(data, 2);
        var isNoteOn = type == 0x90 && velocity > 0;
        var timestamp = checked(_clock.ElapsedTicks * 1_000_000L / Stopwatch.Frequency);

        // 原生回调线程只转换事件
        NoteChanged?.Invoke(new MidiNoteMessage(note, velocity, status & 0x0F, isNoteOn, timestamp));
    }

    private void StopCore()
    {
        if (_input != IntPtr.Zero && CurrentDevice is not null)
            RtMidiNative.skymusic_midi_input_close(_input);
        CurrentDevice = null;
        _clock.Reset();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            StopCore();
            if (_input == IntPtr.Zero)
                return;
            RtMidiNative.skymusic_midi_input_destroy(_input);
            _input = IntPtr.Zero;
        }
    }
}
