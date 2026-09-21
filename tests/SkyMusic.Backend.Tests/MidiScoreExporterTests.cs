// 模块：SkyMusic.Backend.Tests 后端测试 MidiScoreExporterTests
using SkyMusic.Core.Models;
using SkyMusic.Infrastructure.Scores;

namespace SkyMusic.Backend.Tests;

public sealed class MidiScoreExporterTests
{
    [Fact]
    public async Task RoundTripPreservesChromaticNotesAndChannels()
    {
        var score = new Score("chromatic", "author",
        [
            new NoteEvent(60, 0, 250_000, 90, Channel: 1),
            new NoteEvent(61, 300_000, 125_000, 110, Channel: 2),
            new NoteEvent(108, 500_000, 100_000, 127, Channel: 3)
        ]);
        await using var stream = new MemoryStream();
        new MidiScoreExporter().Export(score, stream);
        stream.Position = 0;

        var result = await new MidiScoreImporter().ImportAsync(stream, "roundtrip.mid");

        Assert.True(result.IsSuccess);
        Assert.Equal([60, 61, 108], result.Score!.Notes.Select(note => note.MidiNote));
        Assert.Equal([1, 2, 3], result.Score.Notes.Select(note => note.Channel));
        Assert.Equal([90, 110, 127], result.Score.Notes.Select(note => (int)note.Velocity));
    }

    [Fact]
    public async Task RoundTripPreservesComposerTempoMetadata()
    {
        var score = new Score(
            "tempo",
            string.Empty,
            [new NoteEvent(60, 0, 100_000)],
            new Dictionary<string, string> { ["bpm"] = "220" });
        await using var stream = new MemoryStream();

        new MidiScoreExporter().Export(score, stream);
        stream.Position = 0;
        var result = await new MidiScoreImporter().ImportAsync(stream, "tempo.mid");

        Assert.Equal("220", result.Score!.Metadata!["bpm"]);
    }

    [Fact]
    public async Task RoundTripPreservesLayerTracks()
    {
        var score = new Score("layers", string.Empty,
        [
            new NoteEvent(60, 0, 100_000, Track: 0),
            new NoteEvent(64, 0, 100_000, Track: 1)
        ]);
        await using var stream = new MemoryStream();

        new MidiScoreExporter().Export(score, stream);
        stream.Position = 0;
        var result = await new MidiScoreImporter().ImportAsync(stream, "layers.mid");

        Assert.Equal([0, 1], result.Score!.Notes.Select(note => note.Track));
        Assert.Equal("2", result.Score.Metadata!["trackCount"]);
    }
}
