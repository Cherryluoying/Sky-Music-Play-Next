using SkyMusic.Core.Projects;

namespace SkyMusic.Backend.Tests;

public sealed class MusicProjectEditorTests
{
    private readonly MusicProjectEditor _editor = new();

    [Fact]
    public void AddsDuplicatesAndRemovesTracksWithIndependentNoteIds()
    {
        var project = CreateProject();
        var sourceId = project.Tracks[0].Id;

        project = _editor.DuplicateTrack(project, sourceId);

        Assert.Equal(2, project.Tracks.Count);
        Assert.Equal("Piano Copy", project.Tracks[1].Name);
        Assert.NotEqual(project.Tracks[0].Notes[0].Id, project.Tracks[1].Notes[0].Id);
        project = _editor.RemoveTrack(project, sourceId);
        Assert.Single(project.Tracks);
    }

    [Fact]
    public void MovesResizesAndClampsNotes()
    {
        var project = CreateProject();
        var track = project.Tracks[0];
        var note = track.Notes[0];

        project = _editor.MoveNotes(project, track.Id, [note.Id], -200, 80);
        project = _editor.ResizeNote(project, track.Id, note.Id, 0);

        var updated = Assert.Single(project.Tracks[0].Notes);
        Assert.Equal(0, updated.StartTick);
        Assert.Equal(127, updated.MidiNote);
        Assert.Equal(1, updated.LengthTicks);
    }

    [Fact]
    public void QuantizesStartAndLengthToGrid()
    {
        var project = CreateProject();
        var track = project.Tracks[0];
        var note = track.Notes[0];
        project = _editor.UpdateNote(project, track.Id, note.Id, item => item with
        {
            StartTick = 181,
            LengthTicks = 179
        });

        project = _editor.QuantizeNotes(project, track.Id, [note.Id], 120);

        var updated = Assert.Single(project.Tracks[0].Notes);
        Assert.Equal(240, updated.StartTick);
        Assert.Equal(120, updated.LengthTicks);
    }

    [Fact]
    public void UpdatesMixAndVelocityWithinSupportedRange()
    {
        var project = CreateProject();
        var track = project.Tracks[0];
        var note = track.Notes[0];

        project = _editor.UpdateTrack(project, track.Id, item => item with
        {
            Gain = 9,
            Pan = -4,
            IsMuted = true
        });
        project = _editor.SetVelocity(project, track.Id, [note.Id], 200);

        Assert.Equal(4, project.Tracks[0].Gain);
        Assert.Equal(-1, project.Tracks[0].Pan);
        Assert.True(project.Tracks[0].IsMuted);
        Assert.Equal(127, project.Tracks[0].Notes[0].Velocity);
    }

    private static MusicProject CreateProject()
    {
        var project = MusicProject.Create("Editor");
        return project with
        {
            Tracks =
            [
                new ProjectTrack(
                    Guid.NewGuid(),
                    "Piano",
                    ProjectTrackKind.Instrument,
                    "#3B82F6",
                    [new ProjectNote(Guid.NewGuid(), 120, 240, 60)])
            ]
        };
    }
}
