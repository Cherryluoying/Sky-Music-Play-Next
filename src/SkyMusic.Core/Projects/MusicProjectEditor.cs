// 模块：SkyMusic.Core 工程领域 MusicProjectEditor
namespace SkyMusic.Core.Projects;

public sealed class MusicProjectEditor
{
    public MusicProject AddTrack(
        MusicProject project,
        string name,
        ProjectTrackKind kind = ProjectTrackKind.Instrument,
        string color = "#3B82F6",
        string? instrumentId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Track name is required", nameof(name));

        var track = new ProjectTrack(
            Guid.NewGuid(),
            name.Trim(),
            kind,
            color,
            [],
            instrumentId);
        return Commit(project, project.Tracks.Append(track).ToArray());
    }

    public MusicProject RemoveTrack(MusicProject project, Guid trackId)
    {
        var index = FindTrackIndex(project, trackId);
        var tracks = project.Tracks.Where((_, itemIndex) => itemIndex != index).ToArray();
        return Commit(project, tracks);
    }

    public MusicProject DuplicateTrack(MusicProject project, Guid trackId)
    {
        var index = FindTrackIndex(project, trackId);
        var source = project.Tracks[index];
        var copy = source with
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} Copy",
            Notes = source.Notes.Select(note => note with { Id = Guid.NewGuid() }).ToArray()
        };
        var tracks = project.Tracks.ToList();
        tracks.Insert(index + 1, copy);
        return Commit(project, tracks);
    }

    public MusicProject UpdateTrack(
        MusicProject project,
        Guid trackId,
        Func<ProjectTrack, ProjectTrack> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var index = FindTrackIndex(project, trackId);
        var updated = update(project.Tracks[index]);
        if (updated.Id != trackId)
            throw new ArgumentException("Track update cannot change its id", nameof(update));
        if (string.IsNullOrWhiteSpace(updated.Name))
            throw new ArgumentException("Track name is required", nameof(update));

        var tracks = project.Tracks.ToArray();
        tracks[index] = updated with
        {
            Name = updated.Name.Trim(),
            Gain = Math.Clamp(updated.Gain, 0f, 4f),
            Pan = Math.Clamp(updated.Pan, -1f, 1f),
            Notes = OrderNotes(updated.Notes)
        };
        return Commit(project, tracks);
    }

    public MusicProject AddNote(MusicProject project, Guid trackId, ProjectNote note)
    {
        ValidateNote(note);
        if (project.Tracks.SelectMany(track => track.Notes).Any(item => item.Id == note.Id))
            throw new ArgumentException("Note id must be unique", nameof(note));
        return UpdateTrack(project, trackId, track => track with
        {
            Notes = OrderNotes(track.Notes.Append(note))
        });
    }

    public MusicProject DeleteNotes(MusicProject project, Guid trackId, IEnumerable<Guid> noteIds)
    {
        ArgumentNullException.ThrowIfNull(noteIds);
        var ids = noteIds.ToHashSet();
        if (ids.Count == 0)
            return project;
        return UpdateTrack(project, trackId, track => track with
        {
            Notes = track.Notes.Where(note => !ids.Contains(note.Id)).ToArray()
        });
    }

    public MusicProject UpdateNote(
        MusicProject project,
        Guid trackId,
        Guid noteId,
        Func<ProjectNote, ProjectNote> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        return UpdateTrack(project, trackId, track =>
        {
            var noteIndex = FindNoteIndex(track, noteId);
            var notes = track.Notes.ToArray();
            var next = update(notes[noteIndex]);
            if (next.Id != noteId)
                throw new ArgumentException("Note update cannot change its id", nameof(update));
            ValidateNote(next);
            notes[noteIndex] = next;
            return track with { Notes = OrderNotes(notes) };
        });
    }

    public MusicProject MoveNotes(
        MusicProject project,
        Guid trackId,
        IEnumerable<Guid> noteIds,
        long deltaTicks,
        int deltaSemitones)
    {
        var ids = noteIds.ToHashSet();
        if (ids.Count == 0 || (deltaTicks == 0 && deltaSemitones == 0))
            return project;

        return UpdateTrack(project, trackId, track => track with
        {
            Notes = OrderNotes(track.Notes.Select(note => ids.Contains(note.Id)
                ? note with
                {
                    StartTick = Math.Max(0, note.StartTick + deltaTicks),
                    MidiNote = Math.Clamp(note.MidiNote + deltaSemitones, 0, 127)
                }
                : note))
        });
    }

    public MusicProject ResizeNote(
        MusicProject project,
        Guid trackId,
        Guid noteId,
        long lengthTicks)
        => UpdateNote(project, trackId, noteId, note => note with
        {
            LengthTicks = Math.Max(1, lengthTicks)
        });

    public MusicProject SetVelocity(
        MusicProject project,
        Guid trackId,
        IEnumerable<Guid> noteIds,
        int velocity)
    {
        var ids = noteIds.ToHashSet();
        var value = (byte)Math.Clamp(velocity, 1, 127);
        return UpdateTrack(project, trackId, track => track with
        {
            Notes = track.Notes.Select(note => ids.Contains(note.Id)
                ? note with { Velocity = value }
                : note).ToArray()
        });
    }

    public MusicProject QuantizeNotes(
        MusicProject project,
        Guid trackId,
        IEnumerable<Guid> noteIds,
        long gridTicks,
        bool quantizeLength = true)
    {
        if (gridTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(gridTicks));
        var ids = noteIds.ToHashSet();
        if (ids.Count == 0)
            return project;

        return UpdateTrack(project, trackId, track => track with
        {
            Notes = OrderNotes(track.Notes.Select(note => ids.Contains(note.Id)
                ? note with
                {
                    StartTick = Quantize(note.StartTick, gridTicks),
                    LengthTicks = quantizeLength ? Math.Max(gridTicks, Quantize(note.LengthTicks, gridTicks)) : note.LengthTicks
                }
                : note))
        });
    }

    private static MusicProject Commit(MusicProject project, IReadOnlyList<ProjectTrack> tracks)
    {
        var next = project with
        {
            Tracks = tracks,
            Metadata = project.Metadata with { ModifiedAt = DateTimeOffset.UtcNow }
        };
        MusicProjectValidator.Validate(next);
        return next;
    }

    private static int FindTrackIndex(MusicProject project, Guid trackId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var index = project.Tracks.ToList().FindIndex(track => track.Id == trackId);
        return index >= 0 ? index : throw new KeyNotFoundException($"Track {trackId:D} was not found");
    }

    private static int FindNoteIndex(ProjectTrack track, Guid noteId)
    {
        var index = track.Notes.ToList().FindIndex(note => note.Id == noteId);
        return index >= 0 ? index : throw new KeyNotFoundException($"Note {noteId:D} was not found");
    }

    private static ProjectNote[] OrderNotes(IEnumerable<ProjectNote> notes)
        => notes.OrderBy(note => note.StartTick).ThenBy(note => note.MidiNote).ThenBy(note => note.Id).ToArray();

    private static long Quantize(long value, long grid)
        => Math.Max(0, (long)Math.Round((decimal)value / grid, MidpointRounding.AwayFromZero) * grid);

    private static void ValidateNote(ProjectNote note)
    {
        if (note.Id == Guid.Empty)
            throw new ArgumentException("Note id is required", nameof(note));
        if (note.StartTick < 0 || note.LengthTicks <= 0)
            throw new ArgumentException("Note timing must be positive", nameof(note));
        if (note.MidiNote is < 0 or > 127 || note.Velocity is < 1 or > 127 || note.Channel is < 0 or > 15)
            throw new ArgumentException("Note MIDI data is outside its valid range", nameof(note));
    }
}
