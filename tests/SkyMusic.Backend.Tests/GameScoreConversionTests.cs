// 模块：SkyMusic.Backend.Tests 后端测试 GameScoreConversionTests
using SkyMusic.Core.GameScores;
using SkyMusic.Core.Models;

namespace SkyMusic.Backend.Tests;

public sealed class GameScoreConversionTests
{
    [Fact]
    public void PlaybackProjectionUsesTempoStepsAndLayers()
    {
        var editor = new GameScoreEditor();
        var document = editor.AddInstrument(GameScoreDocument.Create("Play"), "Harp");
        document = editor.ToggleNote(document, 0, 0, 0);
        document = editor.ToggleNote(document, 0, 0, 1);
        document = editor.SetTempoStep(document, 0, 1);
        document = editor.ToggleNote(document, 1, 1, 0);

        var score = new GameScorePlaybackConverter().ToScore(document);

        Assert.Equal(3, score.Notes.Count);
        Assert.Equal(60, score.Notes[0].MidiNote);
        Assert.Equal(2, score.Notes.Count(note => note.StartMicroseconds == 100_000));
        Assert.Equal(236_000, score.Notes.Single(note => note.MidiNote == 62).StartMicroseconds);
    }

    [Fact]
    public void MidiProjectionSeparatesTracksIntoLayersAndFoldsOctaves()
    {
        var score = new Score("Midi", string.Empty,
        [
            new NoteEvent(52, 0, 100_000, 100, 0),
            new NoteEvent(60, 5_000, 100_000, 100, 1)
        ], new Dictionary<string, string>());

        var document = new GameScoreMidiConverter().FromScore(
            score,
            new GameScoreImportOptions(GameScoreProfile.Sky, 120));

        Assert.Equal(2, document.Instruments.Count);
        Assert.Single(document.Columns[0].Notes);
        Assert.Equal(3UL, document.Columns[0].Notes[0].LayerMask);
    }

    [Fact]
    public void MidiProjectionFiltersTracksAndAppliesPerTrackOffsets()
    {
        var score = new Score("Midi", string.Empty,
        [
            new NoteEvent(60, 0, 100_000, 100, 0),
            new NoteEvent(62, 0, 100_000, 100, 1)
        ]);
        var options = new GameScoreImportOptions(
            IncludedTracks: new HashSet<int> { 1 },
            TrackTransposeSemitones: new Dictionary<int, int> { [1] = 2 });

        var result = new GameScoreMidiConverter().Convert(score, options);

        Assert.Equal([1], result.SourceTracks);
        Assert.Equal(1, result.ImportedNoteCount);
        Assert.Equal(2, Assert.Single(result.Document.Columns[0].Notes).KeyIndex);
    }
}
