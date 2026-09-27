// 模块：播放模式、随机演奏与双语歌词的行为回归。
using SkyMusic.Core.Lyrics;
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;

namespace SkyMusic.Backend.Tests;

public sealed class PlaybackEnhancementsTests
{
    [Fact]
    public void RepeatOneAffectsCompletionButNotManualSkip()
    {
        var queue = new PlaybackQueueOrder();
        queue.Replace(["a", "b"]);
        queue.Select("a");
        Assert.Equal("a", queue.Next(QueuePlaybackMode.RepeatOne, automatic: true));
        Assert.Equal("b", queue.Next(QueuePlaybackMode.RepeatOne));
        queue.Select("b");
        Assert.Equal("a", queue.Next(QueuePlaybackMode.ListLoop, automatic: true));
        queue.Remove("b");
        Assert.Equal("a", queue.Next(QueuePlaybackMode.RepeatOne, automatic: true));
    }

    [Fact]
    public void ShuffleExcludesCurrentAndRemovedItems()
    {
        var queue = new PlaybackQueueOrder();
        queue.Replace(["a", "b", "c"]);
        queue.Select("a");
        queue.Remove("b");
        for (var i = 0; i < 20; i++) Assert.Equal("c", queue.Next(QueuePlaybackMode.Shuffle));
        queue.Remove("c");
        Assert.Equal("a", queue.Next(QueuePlaybackMode.Shuffle));
        queue.Remove("a");
        Assert.Null(queue.Next(QueuePlaybackMode.Shuffle));
        queue.Replace(["midi", "midi"]);
        Assert.Single(queue.Items);
        Assert.Equal("midi", queue.Next(QueuePlaybackMode.ListLoop));
    }

    [Fact]
    public void ExplicitNextOverridesModeUntilSelectedOrRemoved()
    {
        var queue = new PlaybackQueueOrder();
        queue.Replace(["a", "b", "c"]);
        queue.Select("a");
        queue.AddNext("c");
        Assert.Equal("c", queue.Next(QueuePlaybackMode.RepeatOne, automatic: true));
        Assert.Equal("c", queue.Next(QueuePlaybackMode.Shuffle));
        queue.Select("c");
        Assert.Equal("c", queue.Next(QueuePlaybackMode.RepeatOne, automatic: true));
        queue.AddNext("b");
        queue.Remove("b");
        Assert.Equal("a", queue.Next(QueuePlaybackMode.Shuffle));
    }

    [Fact]
    public void RandomTimingKeepsChordsTogetherAndWithinConfiguredRange()
    {
        var notes = Enumerable.Range(0, 40).SelectMany(i => new[]
        {
            new NoteEvent(60, i * 200_000L, 100_000), new NoteEvent(64, i * 200_000L, 100_000)
        }).ToArray();
        var result = new ScoreTimelineCompiler().Compile(new Score("随机测试", "", notes), new(5, 10, 20, 15));
        var starts = result.Events.Where(e => e.Type == PlaybackEventType.KeyDown).GroupBy(e => e.TimeMicroseconds).ToArray();
        Assert.Equal(40, starts.Length);
        Assert.All(starts, chord => Assert.Equal(2, chord.Count()));
        for (var i = 1; i < starts.Length; i++) Assert.InRange(starts[i].Key - starts[i - 1].Key, 185_000, 225_000);
        foreach (var note in result.Events.Where(e => e.Type == PlaybackEventType.KeyDown))
        {
            var release = result.Events.First(e => e.Type == PlaybackEventType.KeyUp && e.MidiNote == note.MidiNote && e.TimeMicroseconds > note.TimeMicroseconds);
            Assert.InRange(release.TimeMicroseconds - note.TimeMicroseconds, 95_000, 125_000);
        }
    }

    [Fact]
    public void RandomTimingRejectsInvalidBoundsAndNeverProducesNegativeDurations()
    {
        var compiler = new ScoreTimelineCompiler();
        var score = new Score("测试", "", [new NoteEvent(60, 0, 1_000)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => compiler.Compile(score, new(RandomIntervalMilliseconds: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => compiler.Compile(score, new(RandomReleaseMilliseconds: 1001)));
        var timeline = compiler.Compile(score, new(KeyReleaseDelayMilliseconds: -1000, RandomReleaseMilliseconds: 1000));
        Assert.True(timeline.DurationMicroseconds >= 1000);
    }

    [Fact]
    public void BilingualLyricsKeepOriginalAndTranslationInOneTimedLine()
    {
        var lines = LrcLyricsParser.Parse("[00:01]Hello\n[00:01]你好\n[00:02]Goodbye\n再见");
        Assert.Equal(2, lines.Count);
        Assert.Equal("Hello", lines[0].Text);
        Assert.Equal("你好", lines[0].Translation);
        Assert.Equal("再见", lines[1].Translation);
        var normalized = LyricLines.Normalize([new(TimeSpan.Zero, "Original\nTranslation"), new(TimeSpan.Zero, "Original")]);
        Assert.Single(normalized);
        Assert.Equal("Translation", normalized[0].Translation);
        Assert.Equal("AC/DC", LyricLines.Normalize([new(TimeSpan.Zero, "AC/DC")])[0].Text);
    }
}
