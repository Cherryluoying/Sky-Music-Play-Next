// 模块：SkyMusic.Core 游戏乐谱领域 GameScoreMidiConverter
using SkyMusic.Core.Models;

namespace SkyMusic.Core.GameScores;

public sealed class GameScoreMidiConverter
{
    public GameScoreDocument FromScore(Score score, GameScoreImportOptions? options = null)
        => Convert(score, options).Document;

    // 将 88 键 MIDI 乐谱量化并映射为游戏谱列
    public GameScoreMidiConversionResult Convert(Score score, GameScoreImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(score);
        options ??= new GameScoreImportOptions();
        var bpm = Math.Clamp(options.Bpm, 20, 999);
        var beatMicroseconds = 60_000_000d / bpm;
        var threshold = options.ChordThresholdMicroseconds ?? (long)(beatMicroseconds / 9d);
        var selectedNotes = score.Notes
            .Where(note => options.IncludedTracks is null || options.IncludedTracks.Contains(note.Track))
            .ToArray();
        var trackLayers = selectedNotes.Select(note => note.Track).Distinct().Order().Take(GameScoreDocument.MaxLayers)
            .Select((track, layer) => (track, layer)).ToDictionary(item => item.track, item => item.layer);
        var lowerBound = options.Profile == GameScoreProfile.Sky ? 60 : 48;
        var upperBound = options.Profile == GameScoreProfile.Custom ? 71 : 84;
        var belowRange = 0;
        var aboveRange = 0;
        var accidentals = 0;
        var mapped = selectedNotes
            .Where(note => trackLayers.ContainsKey(note.Track))
            .Select(note =>
            {
                var trackOffset = options.TrackTransposeSemitones?.GetValueOrDefault(note.Track) ?? 0;
                var translatedNote = note.MidiNote + options.TransposeSemitones + trackOffset;
                if (translatedNote < lowerBound)
                    belowRange++;
                if (translatedNote > upperBound)
                    aboveRange++;
                var match = GameKeyLayout.FromMidiNote(
                    options.Profile,
                    translatedNote,
                    options.OctaveFoldCount);
                if (match?.IsAccidental == true)
                    accidentals++;
                return (Note: note, Match: match);
            })
            .Where(item => item.Match is not null && (options.IncludeAccidentals || !item.Match.Value.IsAccidental))
            .OrderBy(item => item.Note.StartMicroseconds)
            .ThenBy(item => item.Match!.Value.KeyIndex)
            .ToArray();

        // MIDI 时间先聚合为和弦 再用四档拍长拆分间隔
        var groups = GroupByTime(mapped, threshold);
        var columns = new List<GameScoreColumn>();
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var masks = new Dictionary<int, ulong>();
            foreach (var item in groups[groupIndex])
            {
                var key = item.Match!.Value.KeyIndex;
                masks[key] = masks.GetValueOrDefault(key) | (1UL << trackLayers[item.Note.Track]);
            }

            var duration = groupIndex + 1 < groups.Count
                ? groups[groupIndex + 1][0].Note.StartMicroseconds - groups[groupIndex][0].Note.StartMicroseconds
                : (long)beatMicroseconds;
            var steps = DecomposeDuration(duration, beatMicroseconds, options.Precision);
            var firstStep = steps.Count > 0 ? steps[0] : 0;
            columns.Add(new GameScoreColumn(
                firstStep,
                masks.OrderBy(pair => pair.Key).Select(pair => new GameScoreNote(pair.Key, pair.Value)).ToArray()));
            foreach (var step in steps.Skip(1))
                columns.Add(new GameScoreColumn(step, []));
        }

        if (columns.Count == 0)
            columns.AddRange(Enumerable.Range(0, 32).Select(_ => GameScoreColumn.Empty));
        var instruments = trackLayers.OrderBy(pair => pair.Value).Select(pair => new GameInstrumentLayer(
            Guid.NewGuid(),
            $"Track {pair.Key + 1}",
            Icon: (GameNoteIcon)(pair.Value % 3))).ToArray();
        if (instruments.Length == 0)
            instruments = [new GameInstrumentLayer(Guid.NewGuid(), options.Profile == GameScoreProfile.Genshin ? "Lyre" : "Piano")];

        var document = new GameScoreDocument(
            score.Title,
            options.Profile,
            bpm,
            "C",
            false,
            instruments,
            columns,
            [0]);
        return new GameScoreMidiConversionResult(
            document,
            mapped.Length,
            accidentals,
            belowRange,
            aboveRange,
            trackLayers.Keys.Order().ToArray());
    }

    private static List<List<(NoteEvent Note, GameKeyMatch? Match)>> GroupByTime(
        IReadOnlyList<(NoteEvent Note, GameKeyMatch? Match)> notes,
        long threshold)
    {
        var groups = new List<List<(NoteEvent Note, GameKeyMatch? Match)>>();
        foreach (var note in notes)
        {
            if (groups.Count == 0 || note.Note.StartMicroseconds - groups[^1][0].Note.StartMicroseconds >= threshold)
                groups.Add([note]);
            else
                groups[^1].Add(note);
        }
        return groups;
    }

    // 用有限节拍步长逼近音符间隔
    private static IReadOnlyList<int> DecomposeDuration(long duration, double beat, int precision)
    {
        var allowed = Math.Clamp(precision, 1, 4);
        var result = new List<int>();
        var remaining = Math.Max(duration, (long)(beat / 8d));
        for (var guard = 0; guard < 10_000 && remaining >= beat / 8d; guard++)
        {
            var selected = 0;
            for (var step = 0; step < allowed; step++)
                if (remaining >= beat * GameTempoSteps.GetRatio(step) - 1)
                {
                    selected = step;
                    break;
                }
            result.Add(selected);
            remaining -= (long)Math.Round(beat * GameTempoSteps.GetRatio(selected));
        }
        if (result.Count == 0)
            result.Add(Math.Min(3, allowed - 1));
        return result;
    }
}
