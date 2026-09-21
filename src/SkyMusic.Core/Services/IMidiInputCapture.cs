// 模块：SkyMusic.Core 桌面服务 IMidiInputCapture
using SkyMusic.Core.Midi;

namespace SkyMusic.Core.Services;

public interface IMidiInputCapture : IDisposable
{
    IReadOnlyList<MidiInputDeviceInfo> Devices { get; }

    MidiInputDeviceInfo? CurrentDevice { get; }

    bool IsListening { get; }

    event Action<MidiNoteMessage>? NoteChanged;

    IReadOnlyList<MidiInputDeviceInfo> RefreshDevices();

    void Start(int deviceIndex);

    void Stop();
}
