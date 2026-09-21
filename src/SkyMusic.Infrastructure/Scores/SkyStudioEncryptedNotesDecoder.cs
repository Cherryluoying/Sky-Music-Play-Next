// 模块：SkyMusic.Infrastructure 通用模型 SkyStudioEncryptedNotesDecoder
using System.Text;
using System.Text.Json;

namespace SkyMusic.Infrastructure.Scores;

public sealed class SkyStudioEncryptedNotesDecoder
{
    private const string Key = "TB,R&Q}-ULFXF7={nU7v?fy#Khr9Mhuu";
    private const string Signature = "ztB_kaFeQe/wa8Kq{r_jz!r=P])hQL(f";

    public JsonElement Decode(JsonElement encryptedNotes)
    {
        if (encryptedNotes.ValueKind != JsonValueKind.Array || encryptedNotes.GetArrayLength() == 0)
        {
            throw new InvalidDataException("Encrypted songNotes must be a non-empty integer array");
        }

        var decrypted = new StringBuilder(encryptedNotes.GetArrayLength());
        var index = 0;
        foreach (var value in encryptedNotes.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var encryptedValue) ||
                encryptedValue is < short.MinValue or > short.MaxValue)
            {
                throw new InvalidDataException($"Encrypted songNotes contains an invalid value at index {index}");
            }

            var character = encryptedValue - Key[index % Key.Length] + 100;
            if (character is < char.MinValue or > char.MaxValue || char.IsSurrogate((char)character))
            {
                throw new InvalidDataException($"Encrypted songNotes contains an invalid character at index {index}");
            }

            decrypted.Append((char)character);
            index++;
        }

        if (!decrypted.ToString().EndsWith(Signature, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Encrypted songNotes signature is missing or invalid");
        }

        decrypted.Length -= Signature.Length;
        try
        {
            using var document = JsonDocument.Parse(decrypted.ToString());
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Decrypted songNotes is not an array");
            }

            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Decrypted songNotes contains invalid JSON", exception);
        }
    }
}
