// 模块：SkyMusic.Core 播放领域 ScoreTimelineCompiler
using System.Collections.Immutable;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.Core.Playback;

public sealed class ScoreTimelineCompiler : IScoreTimelineCompiler
{
    // 把音符转换为稳定排序的按下与释放事件
    public CompiledTimeline Compile(Score score, ScoreTimingSettings? timing = null)
    {
        ArgumentNullException.ThrowIfNull(score);
        timing ??= ScoreTimingSettings.Default;
        Validate(timing);

        var events = ImmutableArray.CreateBuilder<PlaybackEvent>(score.Notes.Count * 2);
        var adjustedStarts = BuildAdjustedStarts(score.Notes, timing.IntervalAdjustmentMilliseconds);

        foreach (var note in score.Notes)
        {
            Validate(note);
            var start = adjustedStarts[note.StartMicroseconds];
            var duration = Math.Max(1_000, note.DurationMicroseconds + (timing.KeyReleaseDelayMilliseconds * 1_000L));
            events.Add(new PlaybackEvent(
                start,
                note.MidiNote,
                PlaybackEventType.KeyDown,
                note.Velocity,
                note.Track,
                note.Channel));
            events.Add(new PlaybackEvent(
                start + duration,
                note.MidiNote,
                PlaybackEventType.KeyUp,
                0,
                note.Track,
                note.Channel));
        }

        events.Sort(PlaybackEventComparer.Instance);
        var timeline = events.MoveToImmutable();
        return new CompiledTimeline(
            timeline,
            timeline.IsEmpty ? 0 : timeline[^1].TimeMicroseconds);
    }

    // 按实时音符间隔累积修正同一时刻的起始时间
    private static IReadOnlyDictionary<long, long> BuildAdjustedStarts(
        IReadOnlyList<NoteEvent> notes,
        int intervalAdjustmentMilliseconds)
    {
        var starts = notes.Select(note => note.StartMicroseconds).Distinct().Order().ToArray();
        var adjusted = new Dictionary<long, long>(starts.Length);
        if (starts.Length == 0)
        {
            return adjusted;
        }

        adjusted[starts[0]] = starts[0];
        var intervalAdjustment = intervalAdjustmentMilliseconds * 1_000L;
        for (var index = 1; index < starts.Length; index++)
        {
            var sourceInterval = starts[index] - starts[index - 1];
            adjusted[starts[index]] = adjusted[starts[index - 1]] + Math.Max(0, sourceInterval + intervalAdjustment);
        }

        return adjusted;
    }

    private static void Validate(ScoreTimingSettings timing)
    {
        if (timing.IntervalAdjustmentMilliseconds is < -1_000 or > 5_000)
        {
            throw new ArgumentOutOfRangeException(nameof(timing), "Interval adjustment must be between -1000 and 5000 ms");
        }

        if (timing.KeyReleaseDelayMilliseconds is < -1_000 or > 5_000)
        {
            throw new ArgumentOutOfRangeException(nameof(timing), "Key release delay must be between -1000 and 5000 ms");
        }
    }

    private static void Validate(NoteEvent note)
    {
        if (note.MidiNote is < 21 or > 108)
        {
            throw new ArgumentOutOfRangeException(nameof(note), note.MidiNote, "MIDI note must be within the 88-key piano range");
        }

        if (note.StartMicroseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(note), note.StartMicroseconds, "Start time cannot be negative");
        }

        if (note.DurationMicroseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(note), note.DurationMicroseconds, "Duration must be positive");
        }

        if (note.Channel is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(note), note.Channel, "MIDI channel must be between 0 and 15");
        }
    }

    private sealed class PlaybackEventComparer : IComparer<PlaybackEvent>
    {
        public static PlaybackEventComparer Instance { get; } = new();

        public int Compare(PlaybackEvent x, PlaybackEvent y)
        {
            var result = x.TimeMicroseconds.CompareTo(y.TimeMicroseconds);
            if (result != 0)
            {
                return result;
            }

            result = x.Type.CompareTo(y.Type);
            if (result != 0)
            {
                return result;
            }

            result = x.MidiNote.CompareTo(y.MidiNote);
            if (result != 0)
            {
                return result;
            }

            result = x.Channel.CompareTo(y.Channel);
            return result != 0 ? result : x.Track.CompareTo(y.Track);
        }
    }
}
