// 模块：SkyMusic.Backend.Tests 后端测试 MusicProjectScoreConverterTests
using SkyMusic.Core.Models;
using SkyMusic.Core.Projects;
using NoteEvent = SkyMusic.Core.Models.NoteEvent;

namespace SkyMusic.Backend.Tests;

public sealed class MusicProjectScoreConverterTests
{
    [Fact]
    public void ProjectsTempoAwareNotesToPlaybackScore()
    {
        var project = CreateProject();

        var score = new MusicProjectScoreConverter().ToScore(project);

        Assert.Equal("Project", score.Title);
        Assert.Collection(score.Notes,
            first =>
            {
                Assert.Equal(0, first.StartMicroseconds);
                Assert.Equal(500_000, first.DurationMicroseconds);
            },
            second =>
            {
                Assert.Equal(500_000, second.StartMicroseconds);
                Assert.Equal(1_000_000, second.DurationMicroseconds);
            });
    }

    [Fact]
    public void CreatesEditableProjectFromLegacyScore()
    {
        var score = new Score("Legacy", "Composer",
        [
            new NoteEvent(60, 0, 500_000, 100, 0, 0),
            new NoteEvent(64, 500_000, 250_000, 90, 1, 1)
        ]);

        var project = new MusicProjectScoreConverter().FromScore(score);

        Assert.Equal(2, project.Tracks.Count);
        Assert.All(project.Tracks.SelectMany(track => track.Notes), note => Assert.NotEqual(Guid.Empty, note.Id));
        Assert.Equal([0L, 480L], project.Tracks.SelectMany(track => track.Notes).Select(note => note.StartTick).Order());
    }

    private static MusicProject CreateProject()
    {
        var now = DateTimeOffset.UnixEpoch;
        return new MusicProject(
            Guid.NewGuid(),
            new ProjectMetadata("Project", "Author", string.Empty, now, now),
            480,
            [new TempoChange(0, 500_000), new TempoChange(480, 1_000_000)],
            [new TimeSignatureChange(0, 4, 4)],
            [
                new ProjectTrack(Guid.NewGuid(), "Piano", ProjectTrackKind.Instrument, "#3B82F6",
                [
                    new ProjectNote(Guid.NewGuid(), 0, 480, 60),
                    new ProjectNote(Guid.NewGuid(), 480, 480, 64)
                ])
            ],
            [],
            []);
    }
}
