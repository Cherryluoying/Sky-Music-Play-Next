// 模块：SkyMusic.Backend.Tests 后端测试 ScoreTimelineCompilerTests
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;

namespace SkyMusic.Backend.Tests;

public sealed class ScoreTimelineCompilerTests
{
    [Fact]
    public void CompilesDeterministicKeyTransitions()
    {
        var score = new Score(
            "Timeline",
            string.Empty,
            [
                new NoteEvent(60, 0, 100_000),
                new NoteEvent(60, 100_000, 200_000),
                new NoteEvent(64, 100_000, 100_000)
            ]);

        var timeline = new ScoreTimelineCompiler().Compile(score);

        Assert.Equal(6, timeline.Events.Length);
        Assert.Equal(PlaybackEventType.KeyUp, timeline.Events[1].Type);
        Assert.Equal(60, timeline.Events[1].MidiNote);
        Assert.Equal(PlaybackEventType.KeyDown, timeline.Events[2].Type);
        Assert.Equal(60, timeline.Events[2].MidiNote);
        Assert.Equal(300_000, timeline.DurationMicroseconds);
    }

    [Fact]
    public void RejectsNotesOutsidePianoRange()
    {
        var score = new Score("Invalid", string.Empty, [new NoteEvent(20, 0, 1_000)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreTimelineCompiler().Compile(score));
    }

    [Fact]
    public void AppliesPerScoreIntervalAndReleaseDelay()
    {
        var score = new Score(
            "Timing",
            string.Empty,
            [
                new NoteEvent(60, 0, 100_000),
                new NoteEvent(62, 200_000, 100_000),
                new NoteEvent(64, 400_000, 100_000)
            ]);

        var timeline = new ScoreTimelineCompiler().Compile(
            score,
            new ScoreTimingSettings(50, 25));
        var noteDowns = timeline.Events.Where(item => item.Type == PlaybackEventType.KeyDown).ToArray();
        var noteUps = timeline.Events.Where(item => item.Type == PlaybackEventType.KeyUp).ToArray();

        Assert.Equal([0, 250_000, 500_000], noteDowns.Select(item => item.TimeMicroseconds));
        Assert.Equal([125_000, 375_000, 625_000], noteUps.Select(item => item.TimeMicroseconds));
    }
}
