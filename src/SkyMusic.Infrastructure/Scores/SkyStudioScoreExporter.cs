// 模块：SkyMusic.Infrastructure 通用模型 SkyStudioScoreExporter
using System.Text.Json;
using SkyMusic.Core.Models;

namespace SkyMusic.Infrastructure.Scores;

public sealed class SkyStudioScoreExporter
{
    private static readonly IReadOnlyDictionary<int, int> ScaleIndexBySemitone =
        new Dictionary<int, int> { [0] = 0, [2] = 1, [4] = 2, [5] = 3, [7] = 4, [9] = 5, [11] = 6 };

    public async ValueTask<int> ExportAsync(
        Score score,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(destination);

        var notes = new List<object>(score.Notes.Count);
        var skipped = 0;
        foreach (var note in score.Notes)
        {
            if (!TryGetKeyIndex(note.MidiNote, out var keyIndex))
            {
                skipped++;
                continue;
            }

            notes.Add(new
            {
                time = note.StartMicroseconds / 1_000,
                key = $"Key{keyIndex}",
                duration = Math.Max(1, note.DurationMicroseconds / 1_000)
            });
        }

        var document = new[]
        {
            new
            {
                name = score.Title,
                author = score.Composer,
                transcribedBy = "SkyMusicPlay",
                isEncrypted = false,
                pitchLevel = 0,
                songNotes = notes
            }
        };
        await JsonSerializer.SerializeAsync(destination, document, new JsonSerializerOptions
        {
            WriteIndented = true
        }, cancellationToken);
        return skipped;
    }

    private static bool TryGetKeyIndex(int midiNote, out int keyIndex)
    {
        var relative = midiNote - 60;
        var octave = (int)Math.Floor(relative / 12d);
        var semitone = relative - (octave * 12);
        if (!ScaleIndexBySemitone.TryGetValue(semitone, out var scaleIndex))
        {
            keyIndex = 0;
            return false;
        }

        keyIndex = (octave * 7) + scaleIndex;
        return true;
    }
}
