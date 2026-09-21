// 模块：SkyMusic.Infrastructure 输入设备 MidiOutputDeviceCatalog
namespace SkyMusic.Infrastructure.Input;

public sealed class MidiOutputDeviceCatalog
{
    public IReadOnlyList<MidiOutputDeviceInfo> GetAvailableDevices()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        RtMidiNative.EnsureCompatible();
        var count = RtMidiNative.skymusic_midi_output_port_count();
        if (count < 0)
            throw new IOException("RtMidi output devices could not be enumerated");
        return Enumerable.Range(0, count)
            .Select(index => new MidiOutputDeviceInfo(index, RtMidiNative.GetOutputPortName(index)))
            .ToArray();
    }
}
