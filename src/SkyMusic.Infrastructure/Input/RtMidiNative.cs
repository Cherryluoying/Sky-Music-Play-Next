// 模块：SkyMusic.Infrastructure 输入设备 RtMidiNative
using System.Runtime.InteropServices;
using System.Text;

namespace SkyMusic.Infrastructure.Input;

internal static class RtMidiNative
{
    private const string LibraryName = "SkyMusic.Midi";
    internal const uint AbiVersion = 1;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void MessageCallback(IntPtr userData, double deltaSeconds, IntPtr data, uint size);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint skymusic_midi_abi_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int skymusic_midi_input_port_count();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int skymusic_midi_output_port_count();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int skymusic_midi_input_port_name(uint index, byte[]? destination, uint capacity);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int skymusic_midi_output_port_name(uint index, byte[]? destination, uint capacity);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr skymusic_midi_input_create(MessageCallback callback, IntPtr userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int skymusic_midi_input_open(IntPtr input, uint portIndex);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void skymusic_midi_input_close(IntPtr input);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void skymusic_midi_input_destroy(IntPtr input);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr skymusic_midi_output_create();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int skymusic_midi_output_open(IntPtr output, uint portIndex);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int skymusic_midi_output_send(IntPtr output, byte[] data, uint size);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void skymusic_midi_output_close(IntPtr output);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void skymusic_midi_output_destroy(IntPtr output);

    internal static void EnsureCompatible()
    {
        var actual = skymusic_midi_abi_version();
        if (actual != AbiVersion)
            throw new NotSupportedException($"SkyMusic.Midi ABI {actual} is not supported");
    }

    internal static string GetInputPortName(int index) => GetPortName(index, true);

    internal static string GetOutputPortName(int index) => GetPortName(index, false);

    private static string GetPortName(int index, bool input)
    {
        var required = input
            ? skymusic_midi_input_port_name((uint)index, null, 0)
            : skymusic_midi_output_port_name((uint)index, null, 0);
        if (required <= 0)
            throw new IOException("MIDI port name could not be read");

        var buffer = new byte[required];
        var result = input
            ? skymusic_midi_input_port_name((uint)index, buffer, (uint)buffer.Length)
            : skymusic_midi_output_port_name((uint)index, buffer, (uint)buffer.Length);
        if (result <= 0)
            throw new IOException("MIDI port name could not be read");
        var length = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length);
    }
}
