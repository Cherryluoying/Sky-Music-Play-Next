// 模块：SkyMusic.Backend.Tests 后端测试 GenshinMusicGameScoreStoreTests
using System.Text;
using SkyMusic.Core.GameScores;
using SkyMusic.Infrastructure.GameScores;

namespace SkyMusic.Backend.Tests;

public sealed class GenshinMusicGameScoreStoreTests
{
    private readonly GenshinMusicGameScoreStore _store = new();

    [Fact]
    public async Task ImportsVersionOneBinaryLayers()
    {
        await using var stream = JsonStream("""
            [{
              "name":"legacy-composed", "bpm":220,
              "data":{"isComposedVersion":true,"appName":"Sky"},
              "instruments":["Piano","Harp","Trumpet"],
              "columns":[[1,[[4,"100"],[7,"011"]]]],
              "breakpoints":[0]
            }]
            """);

        var document = await _store.LoadAsync(stream);

        Assert.Equal(GameScoreProfile.Sky, document.Profile);
        Assert.Equal(1UL, document.Columns[0].Notes[0].LayerMask);
        Assert.Equal(6UL, document.Columns[0].Notes[1].LayerMask);
    }

    [Fact]
    public async Task ImportsVersionThreeHexLayersAndInstrumentSettings()
    {
        await using var stream = JsonStream("""
            [{
              "name":"modern", "type":"composed", "version":3, "bpm":180, "pitch":"D",
              "data":{"isComposedVersion":true,"appName":"Genshin"},
              "instruments":[{"name":"Lyre","volume":91,"pitch":"C","visible":true,"icon":"line","alias":"Lead","muted":false,"reverbOverride":true}],
              "columns":[[0,[[14,"1"]]],[3,[]]], "breakpoints":[0,1]
            }]
            """);

        var document = await _store.LoadAsync(stream);

        Assert.Equal(GameScoreProfile.Genshin, document.Profile);
        Assert.Equal("Lead", document.Instruments[0].Alias);
        Assert.Equal(GameNoteIcon.Line, document.Instruments[0].Icon);
        Assert.True(document.Instruments[0].ReverbOverride);
    }

    [Fact]
    public async Task ImportsRecordedAndLegacySongs()
    {
        await using var recorded = JsonStream("""
            [{"name":"recorded","version":1,"bpm":200,
              "notes":[[0,100],[1,400],[2,700]],
              "data":{"isComposedVersion":false,"appName":"Sky"}}]
            """);
        await using var legacy = JsonStream("""
            [{"name":"old","bpm":200,"isComposed":"false",
              "songNotes":[{"time":100,"key":"1Key0"},{"time":400,"key":"2Key1"}]}]
            """);

        var recordedDocument = await _store.LoadAsync(recorded);
        var legacyDocument = await _store.LoadAsync(legacy);

        Assert.Equal(3, recordedDocument.Columns.Count(column => column.Notes.Count > 0));
        Assert.Equal(2UL, legacyDocument.Columns.SelectMany(column => column.Notes).Single(note => note.KeyIndex == 1).LayerMask);
    }

    [Fact]
    public async Task ModernExportRoundTripsAllComposerState()
    {
        var editor = new GameScoreEditor();
        var source = GameScoreDocument.Create("Roundtrip", GameScoreProfile.Genshin);
        source = editor.AddInstrument(source, "Zither");
        source = editor.ToggleNote(source, 0, 14, 0);
        source = editor.ToggleNote(source, 0, 14, 1);
        source = editor.SetTempoStep(source, 0, 3);
        source = editor.ToggleBreakpoint(source, 4);
        await using var stream = new MemoryStream();

        await _store.SaveAsync(source, stream);
        stream.Position = 0;
        var restored = await _store.LoadAsync(stream);

        Assert.Equal(source.Name, restored.Name);
        Assert.Equal(source.Profile, restored.Profile);
        Assert.Equal(source.Bpm, restored.Bpm);
        Assert.Equal(source.Breakpoints, restored.Breakpoints);
        Assert.Equal(source.Columns[0].TempoStep, restored.Columns[0].TempoStep);
        Assert.Equal(source.Columns[0].Notes[0].LayerMask, restored.Columns[0].Notes[0].LayerMask);
    }

    [Theory]
    [InlineData("new-format-composed.skysheet.json")]
    [InlineData("new-format-composed-genshin.genshinsheet.json")]
    [InlineData("new-format-recorded.skysheet.json")]
    [InlineData("new-format-recorded-genshin.genshinsheet.json")]
    [InlineData("old-format-composed.json")]
    [InlineData("old-format-recorded.json")]
    public async Task ImportsUpstreamCompatibilityFixtures(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "genshin-music-fixtures", fileName);
        Assert.True(File.Exists(path), $"Missing fixture {path}");
        await using var stream = File.OpenRead(path);

        var document = await _store.LoadAsync(stream);

        Assert.NotEmpty(document.Columns);
        Assert.Contains(document.Columns, column => column.Notes.Count > 0);
        Assert.NotEmpty(document.Instruments);
    }

    private static MemoryStream JsonStream(string json) => new(Encoding.UTF8.GetBytes(json));
}
