// 模块：SkyMusic.Backend.Tests 后端测试 JsonMusicProjectStoreTests
using System.Text;
using SkyMusic.Core.Projects;
using SkyMusic.Core.GameScores;
using SkyMusic.Infrastructure.Projects;

namespace SkyMusic.Backend.Tests;

public sealed class JsonMusicProjectStoreTests
{
    [Fact]
    public async Task RoundTripsVersionedProject()
    {
        var project = MusicProject.Create("Roundtrip") with
        {
            Tracks =
            [
                new ProjectTrack(Guid.NewGuid(), "Piano", ProjectTrackKind.Instrument, "#3B82F6",
                [
                    new ProjectNote(Guid.NewGuid(), 120, 240, 72, 96)
                ])
            ]
        };
        var store = new JsonMusicProjectStore();
        await using var stream = new MemoryStream();

        await store.SaveAsync(project, stream);
        stream.Position = 0;
        var restored = await store.LoadAsync(stream);

        Assert.Equal(project.Id, restored.Id);
        Assert.Equal(project.Metadata, restored.Metadata);
        Assert.Equal(project.Ppq, restored.Ppq);
        Assert.Equal(project.TempoMap, restored.TempoMap);
        Assert.Equal(project.TimeSignatures, restored.TimeSignatures);
        Assert.Equal(project.Tracks[0].Id, restored.Tracks[0].Id);
        Assert.Equal(project.Tracks[0].Notes[0], restored.Tracks[0].Notes[0]);
    }

    [Fact]
    public async Task RejectsUnknownFormatVersion()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            { "formatVersion": 99, "project": null }
            """));

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await new JsonMusicProjectStore().LoadAsync(stream));
    }

    [Fact]
    public async Task RoundTripsEmbeddedGameScore()
    {
        var score = GameScoreDocument.Create("Sheet");
        var project = MusicProject.Create("Project") with
        {
            GameArrangements =
            [
                new GameArrangement(
                    Guid.NewGuid(), "Sky", "Sky", 0, OutOfRangePolicy.Keep,
                    [], new Dictionary<Guid, int>(), GameScore: score)
            ]
        };
        await using var stream = new MemoryStream();

        await new JsonMusicProjectStore().SaveAsync(project, stream);
        stream.Position = 0;
        var restored = await new JsonMusicProjectStore().LoadAsync(stream);

        Assert.Equal(32, Assert.Single(restored.GameArrangements).GameScore!.Columns.Count);
    }
}
