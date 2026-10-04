// 模块：SkyMusic.Core 桌面服务 IMidiInputCapture
using SkyMusic.Core.Midi;

namespace SkyMusic.Core.Services;

public interface IMidiInputCapture : IDisposable
{
    IReadOnlyList<MidiInputDeviceInfo> Devices { get; }

    MidiInputDeviceInfo? CurrentDevice { get; }

    bool IsListening { get; }

    event Action<MidiNoteMessage>? NoteChanged;

    // 完整通道事件供乐器转发；NoteChanged 仍负责录制和钢琴显示。
    event Action<MidiChannelMessage>? MessageReceived;

    IReadOnlyList<MidiInputDeviceInfo> RefreshDevices();

    void Start(int deviceIndex);

    void Stop();
}
