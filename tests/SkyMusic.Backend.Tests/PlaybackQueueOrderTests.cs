// 模块：独立播放队列回归，防止收藏或元数据刷新覆盖用户的队列操作。
using SkyMusic.Core.Playback;

namespace SkyMusic.Backend.Tests;

public sealed class PlaybackQueueOrderTests
{
    [Fact]
    public void PlayNextMovesExistingTrackWithoutDuplicatesAndSurvivesRefresh()
    {
        var queue = new PlaybackQueueOrder();
        queue.Merge(["a", "b", "c", "d"]);
        queue.Select("a");
        queue.AddNext("d");
        queue.Merge(["a", "b", "c", "d", "new"]);
        Assert.Equal(new[] { "a", "d", "b", "c", "new" }, queue.Items);
        Assert.Equal("d", queue.Adjacent(1));
        queue.AddNext("a");
        Assert.Equal(5, queue.Items.Count);
    }

    [Fact]
    public void RemovedTrackStaysRemovedUntilExplicitlyAddedOrPlayed()
    {
        var queue = new PlaybackQueueOrder();
        queue.Merge(["a", "b", "c"]);
        queue.Select("a");
        queue.Remove("b");
        queue.Merge(["a", "b", "c"]);
        Assert.Equal(new[] { "a", "c" }, queue.Items);
        queue.AddNext("b");
        Assert.Equal("b", queue.Adjacent(1));
    }

    [Fact]
    public void RemovingCurrentTrackKeepsNextPositionIncludingLaterEdits()
    {
        var queue = new PlaybackQueueOrder();
        queue.Merge(["a", "b", "c", "d"]);
        queue.Select("b");
        queue.Remove("b");
        Assert.Equal("c", queue.Adjacent(1));
        Assert.Equal("a", queue.Adjacent(-1));
        queue.Remove("a");
        Assert.Equal("c", queue.Adjacent(1));
        queue.AddNext("new");
        Assert.Equal("new", queue.Adjacent(1));
        foreach (var id in queue.Items.ToArray()) queue.Remove(id);
        Assert.Null(queue.Adjacent(1));
        queue.AddNext("first");
        Assert.Equal("first", queue.Adjacent(1));
    }
}
