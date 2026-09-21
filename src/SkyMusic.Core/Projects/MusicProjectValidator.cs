// 模块：SkyMusic.Core 工程领域 MusicProjectValidator
using SkyMusic.Core.GameScores;

namespace SkyMusic.Core.Projects;

public static class MusicProjectValidator
{
    public static void Validate(MusicProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Id == Guid.Empty)
            throw new ProjectValidationException("Project id is required");
        if (project.Ppq is < 24 or > 96_000)
            throw new ProjectValidationException("Project PPQ is outside the supported range");
        if (string.IsNullOrWhiteSpace(project.Metadata.Title))
            throw new ProjectValidationException("Project title is required");
        if (project.TempoMap.Count == 0 || project.TempoMap[0].Tick != 0)
            throw new ProjectValidationException("Tempo map must start at tick zero");

        ValidateTempoMap(project.TempoMap);
        ValidateTimeSignatures(project.TimeSignatures);

        var trackIds = new HashSet<Guid>();
        var noteIds = new HashSet<Guid>();
        foreach (var track in project.Tracks)
        {
            if (track.Id == Guid.Empty || !trackIds.Add(track.Id))
                throw new ProjectValidationException("Track ids must be unique");
            if (track.Gain is < 0f or > 4f || track.Pan is < -1f or > 1f)
                throw new ProjectValidationException("Track gain or pan is outside the supported range");

            foreach (var note in track.Notes)
            {
                if (note.Id == Guid.Empty || !noteIds.Add(note.Id))
                    throw new ProjectValidationException("Note ids must be unique");
                if (note.StartTick < 0 || note.LengthTicks <= 0)
                    throw new ProjectValidationException("Note timing must be positive");
                if (note.MidiNote is < 0 or > 127 || note.Velocity > 127 || note.Channel is < 0 or > 15)
                    throw new ProjectValidationException("Note MIDI data is outside its valid range");
            }
        }

        var arrangementIds = new HashSet<Guid>();
        foreach (var arrangement in project.GameArrangements)
        {
            if (arrangement.Id == Guid.Empty || !arrangementIds.Add(arrangement.Id))
                throw new ProjectValidationException("Game arrangement ids must be unique");
            if (arrangement.GameScore is not null)
                GameScoreValidator.Validate(arrangement.GameScore);
        }
    }

    private static void ValidateTempoMap(IReadOnlyList<TempoChange> tempoMap)
    {
        long previousTick = -1;
        foreach (var tempo in tempoMap)
        {
            if (tempo.Tick <= previousTick || tempo.MicrosecondsPerQuarterNote is < 10_000 or > 60_000_000)
                throw new ProjectValidationException("Tempo map is invalid");
            previousTick = tempo.Tick;
        }
    }

    private static void ValidateTimeSignatures(IReadOnlyList<TimeSignatureChange> signatures)
    {
        long previousTick = -1;
        foreach (var signature in signatures)
        {
            if (signature.Tick <= previousTick || signature.Numerator is < 1 or > 64 ||
                signature.Denominator is < 1 or > 64 || !IsPowerOfTwo(signature.Denominator))
                throw new ProjectValidationException("Time signature map is invalid");
            previousTick = signature.Tick;
        }
    }

    private static bool IsPowerOfTwo(int value) => (value & (value - 1)) == 0;
}
