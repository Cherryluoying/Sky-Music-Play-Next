// 模块：SkyMusic.Core 游戏乐谱领域 GameScorePlaybackConverter
using SkyMusic.Core.Models;
using ScoreNoteEvent = SkyMusic.Core.Models.NoteEvent;

namespace SkyMusic.Core.GameScores;

public sealed class GameScorePlaybackConverter
{
    public Score ToScore(GameScoreDocument document, int startOffsetMilliseconds = 100)
    {
        GameScoreValidator.Validate(document);
        var notes = new List<ScoreNoteEvent>();
        long time = startOffsetMilliseconds * 1_000L;

        foreach (var column in document.Columns)
        {
            var duration = GameTempoSteps.GetDurationMilliseconds(document.Bpm, column.TempoStep) * 1_000L;
            foreach (var note in column.Notes)
            {
                for (var layer = 0; layer < document.Instruments.Count; layer++)
                {
                    if (!note.HasLayer(layer) || document.Instruments[layer].IsMuted)
                        continue;
                    notes.Add(new ScoreNoteEvent(
                        GameKeyLayout.ToMidiNote(document.Profile, note.KeyIndex),
                        time,
                        Math.Max(30_000, Math.Min(duration, 800_000)),
                        (byte)Math.Clamp(document.Instruments[layer].Volume, 1, 127),
                        layer,
                        layer % 16));
                }
            }
            time += duration;
        }

        return new Score(
            document.Name,
            string.Empty,
            notes.OrderBy(note => note.StartMicroseconds).ThenBy(note => note.MidiNote).ToArray(),
            new Dictionary<string, string>
            {
                ["sourceFormat"] = "genshin-music-composed",
                ["gameProfile"] = document.Profile.ToString(),
                ["bpm"] = document.Bpm.ToString()
            });
    }
}
