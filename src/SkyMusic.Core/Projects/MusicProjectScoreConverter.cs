// 模块：SkyMusic.Core 工程领域 MusicProjectScoreConverter
using SkyMusic.Core.Models;
using ScoreNoteEvent = SkyMusic.Core.Models.NoteEvent;

namespace SkyMusic.Core.Projects;

public sealed class MusicProjectScoreConverter
{
    public Score ToScore(MusicProject project)
    {
        MusicProjectValidator.Validate(project);
        var converter = new TickTimeConverter(project.Ppq, project.TempoMap);
        var hasSolo = project.Tracks.Any(track => track.IsSolo);
        var notes = project.Tracks
            .Select((track, trackIndex) => (track, trackIndex))
            .Where(item => hasSolo ? item.track.IsSolo : !item.track.IsMuted)
            .SelectMany(item => item.track.Notes.Select(note => ToScoreNote(note, item.trackIndex, item.track.Gain, converter)))
            .OrderBy(note => note.StartMicroseconds)
            .ThenBy(note => note.MidiNote)
            .ToArray();
        var metadata = new Dictionary<string, string>
        {
            ["sourceFormat"] = "skymusic-project",
            ["projectId"] = project.Id.ToString("D"),
            ["ppq"] = project.Ppq.ToString()
        };
        return new Score(project.Metadata.Title, project.Metadata.Author, notes, metadata);
    }

    public MusicProject FromScore(Score score, int ppq = MusicProject.DefaultPpq)
    {
        ArgumentNullException.ThrowIfNull(score);
        var tempoMap = new[] { new TempoChange(0, 500_000) };
        var converter = new TickTimeConverter(ppq, tempoMap);
        var tracks = score.Notes
            .GroupBy(note => note.Track)
            .OrderBy(group => group.Key)
            .Select((group, index) => new ProjectTrack(
                Guid.NewGuid(),
                $"Track {index + 1}",
                group.Any(note => note.Channel == 9) ? ProjectTrackKind.Percussion : ProjectTrackKind.Instrument,
                TrackColor(index),
                group.Select(note => ToProjectNote(note, converter)).ToArray()))
            .ToArray();
        var now = DateTimeOffset.UtcNow;
        var project = new MusicProject(
            Guid.NewGuid(),
            new ProjectMetadata(score.Title, score.Composer, string.Empty, now, now),
            ppq,
            tempoMap,
            [new TimeSignatureChange(0, 4, 4)],
            tracks,
            [],
            []);
        MusicProjectValidator.Validate(project);
        return project;
    }

    private static ScoreNoteEvent ToScoreNote(ProjectNote note, int trackIndex, float gain, TickTimeConverter converter)
    {
        var start = converter.TickToMicroseconds(note.StartTick);
        var end = converter.TickToMicroseconds(note.EndTick);
        var velocity = (byte)Math.Clamp((int)Math.Round(note.Velocity * gain), 1, 127);
        return new ScoreNoteEvent(note.MidiNote, start, Math.Max(1, end - start), velocity, trackIndex, note.Channel);
    }

    private static ProjectNote ToProjectNote(ScoreNoteEvent note, TickTimeConverter converter)
    {
        var start = converter.MicrosecondsToTick(note.StartMicroseconds);
        var end = converter.MicrosecondsToTick(note.EndMicroseconds);
        return new ProjectNote(Guid.NewGuid(), start, Math.Max(1, end - start), note.MidiNote, note.Velocity, note.Channel);
    }

    private static string TrackColor(int index) => (index % 6) switch
    {
        0 => "#3B82F6",
        1 => "#8B5CF6",
        2 => "#10B981",
        3 => "#F59E0B",
        4 => "#06B6D4",
        _ => "#EC4899"
    };
}
