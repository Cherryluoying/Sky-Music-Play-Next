// 模块：SkyMusic.Backend.Tests 后端测试 SkyStudioScoreExporterTests
using System.Text;
using System.Text.Json;
using SkyMusic.Core.Models;
using SkyMusic.Infrastructure.Scores;

namespace SkyMusic.Backend.Tests;

public sealed class SkyStudioScoreExporterTests
{
    [Fact]
    public async Task ExportsNaturalNotesAndReportsUnsupportedSemitones()
    {
        var score = new Score("test", "author",
        [
            new NoteEvent(60, 0, 250_000),
            new NoteEvent(61, 300_000, 250_000),
            new NoteEvent(62, 600_000, 250_000)
        ]);
        await using var stream = new MemoryStream();

        var skipped = await new SkyStudioScoreExporter().ExportAsync(score, stream);

        Assert.Equal(1, skipped);
        var json = Encoding.UTF8.GetString(stream.ToArray());
        using var document = JsonDocument.Parse(json);
        Assert.Equal(2, document.RootElement[0].GetProperty("songNotes").GetArrayLength());
        Assert.Equal("Key0", document.RootElement[0].GetProperty("songNotes")[0].GetProperty("key").GetString());
    }
}
