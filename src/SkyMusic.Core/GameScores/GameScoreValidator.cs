// 模块：SkyMusic.Core 游戏乐谱领域 GameScoreValidator
namespace SkyMusic.Core.GameScores;

public static class GameScoreValidator
{
    private static readonly HashSet<string> Pitches =
        ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    public static void Validate(GameScoreDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(document.Name))
            throw new ArgumentException("Game score name is required", nameof(document));
        if (document.Bpm is < 20 or > 999)
            throw new ArgumentException("Game score BPM is outside the supported range", nameof(document));
        if (!Pitches.Contains(document.Pitch))
            throw new ArgumentException("Game score pitch is invalid", nameof(document));
        if (document.Instruments.Count is < 1 or > GameScoreDocument.MaxLayers)
            throw new ArgumentException("Game score layer count is outside the supported range", nameof(document));
        if (document.Columns.Count == 0)
            throw new ArgumentException("Game score must contain at least one column", nameof(document));

        foreach (var column in document.Columns)
        {
            if (column.TempoStep is < 0 or > 3)
                throw new ArgumentException("Game score tempo step is invalid", nameof(document));
            if (column.Notes.GroupBy(note => note.KeyIndex).Any(group => group.Count() > 1))
                throw new ArgumentException("A column cannot contain duplicate key indexes", nameof(document));
            foreach (var note in column.Notes)
            {
                if (note.KeyIndex < 0 || note.KeyIndex >= document.KeyCount || note.LayerMask == 0)
                    throw new ArgumentException("Game score note is invalid", nameof(document));
                if ((note.LayerMask >> document.Instruments.Count) != 0)
                    throw new ArgumentException("Game score note references a missing layer", nameof(document));
            }
        }

        if (document.Breakpoints.Any(value => value < 0 || value >= document.Columns.Count) ||
            document.Breakpoints.Distinct().Count() != document.Breakpoints.Count)
            throw new ArgumentException("Game score breakpoints are invalid", nameof(document));
    }
}
