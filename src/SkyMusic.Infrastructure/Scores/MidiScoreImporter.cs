// 模块：SkyMusic.Infrastructure 通用模型 MidiScoreImporter
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using SkyMusic.Core.Importing;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;
using ScoreNoteEvent = SkyMusic.Core.Models.NoteEvent;

namespace SkyMusic.Infrastructure.Scores;

public sealed class MidiScoreImporter : IScoreImporter
{
    private static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mid",
        ".midi"
    };

    private readonly MidiImportOptions _options;

    public MidiScoreImporter(MidiImportOptions? options = null)
    {
        _options = options ?? new MidiImportOptions();
        if (_options.MinimumNote < 0 || _options.MaximumNote > 127 || _options.MinimumNote > _options.MaximumNote)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Invalid MIDI note range");
        }
    }

    public IReadOnlySet<string> SupportedExtensions => Extensions;

    // 合并所选 MIDI 轨道并转换到统一微秒时间轴
    public ValueTask<ScoreImportResult> ImportAsync(
        Stream source,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            var midiFile = MidiFile.Read(source);
            var tempoMap = midiFile.GetTempoMap();
            var notes = new List<ScoreNoteEvent>();
            var skipped = 0;
            var trackIndex = 0;

            foreach (var track in midiFile.GetTrackChunks())
            {
                foreach (var note in track.GetNotes())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var midiNote = (int)note.NoteNumber;
                    var channel = (int)note.Channel;
                    if (midiNote < _options.MinimumNote || midiNote > _options.MaximumNote ||
                        (_options.ExcludePercussionChannel && channel == 9))
                    {
                        skipped++;
                        continue;
                    }

                    var start = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, tempoMap).TotalMicroseconds;
                    var length = LengthConverter.ConvertTo<MetricTimeSpan>(note.Length, note.Time, tempoMap).TotalMicroseconds;
                    notes.Add(new ScoreNoteEvent(
                        midiNote,
                        start,
                        Math.Max(1, length),
                        (byte)note.Velocity,
                        trackIndex,
                        channel));
                }

                trackIndex++;
            }

            if (notes.Count == 0)
            {
                return ValueTask.FromResult(ScoreImportResult.Failed(
                    Error("empty_midi", "No playable notes were found in the MIDI file")));
            }

            notes.Sort(static (left, right) =>
            {
                var result = left.StartMicroseconds.CompareTo(right.StartMicroseconds);
                return result != 0 ? result : left.MidiNote.CompareTo(right.MidiNote);
            });

            var issues = new List<ScoreImportIssue>();
            if (skipped > 0)
            {
                issues.Add(new ScoreImportIssue(
                    "midi_notes_skipped",
                    $"Skipped {skipped} notes outside the selected range or on the percussion channel",
                    ScoreImportIssueSeverity.Warning));
            }

            var metadata = new Dictionary<string, string>
            {
                ["sourceFormat"] = "midi",
                ["sourceName"] = sourceName,
                ["trackCount"] = trackIndex.ToString(),
                ["bpm"] = Math.Round(60_000_000d / tempoMap.GetTempoAtTime(new MidiTimeSpan(0)).MicrosecondsPerQuarterNote, 3).ToString()
            };
            var score = new Score(Path.GetFileNameWithoutExtension(sourceName), string.Empty, notes, metadata);
            return ValueTask.FromResult(new ScoreImportResult(score, issues));
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IOException)
        {
            return ValueTask.FromResult(ScoreImportResult.Failed(
                Error("invalid_midi", $"Unable to read MIDI file: {exception.Message}")));
        }
    }

    private static ScoreImportIssue Error(string code, string message) =>
        new(code, message, ScoreImportIssueSeverity.Error);
}
