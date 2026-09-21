// 模块：SkyMusic.Infrastructure 通用模型 NativeAudioPreview
using System.Runtime.InteropServices;

namespace SkyMusic.Infrastructure.Audio;

internal static class NativeAudioPreview
{
    private const string LibraryName = "SkyMusic.AudioPreview";
    internal const uint AbiVersion = 1;

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint skymusic_audio_preview_abi_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr skymusic_audio_preview_create();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void skymusic_audio_preview_destroy(IntPtr preview);

    [DllImport(
        LibraryName,
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Unicode,
        ExactSpelling = true)]
    internal static extern int skymusic_audio_preview_load(IntPtr preview, uint slot, string path);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int skymusic_audio_preview_trigger(IntPtr preview, uint slot, float gain);
}
