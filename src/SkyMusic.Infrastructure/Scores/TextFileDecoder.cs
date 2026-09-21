// 模块：SkyMusic.Infrastructure 通用模型 TextFileDecoder
using System.Text;

namespace SkyMusic.Infrastructure.Scores;

internal static class TextFileDecoder
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Encoding.UTF8.Preamble))
        {
            return Encoding.UTF8.GetString(bytes[Encoding.UTF8.Preamble.Length..]);
        }

        if (bytes.StartsWith(Encoding.Unicode.Preamble))
        {
            return Encoding.Unicode.GetString(bytes[Encoding.Unicode.Preamble.Length..]);
        }

        if (bytes.StartsWith(Encoding.BigEndianUnicode.Preamble))
        {
            return Encoding.BigEndianUnicode.GetString(bytes[Encoding.BigEndianUnicode.Preamble.Length..]);
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(936).GetString(bytes);
        }
    }
}
