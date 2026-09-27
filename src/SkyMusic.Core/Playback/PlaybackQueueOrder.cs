// 模块：独立播放队列顺序；曲库刷新只补充新曲目，不复活用户移除的条目。
namespace SkyMusic.Core.Playback;

public enum QueuePlaybackMode { ListLoop, RepeatOne, Shuffle }

public sealed class PlaybackQueueOrder
{
    private readonly List<string> _items = [];
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _requestedNext = [];
    private string? _currentId;
    private int _nextAfterRemoval;
    public IReadOnlyList<string> Items => _items;

    // 从筛选后的歌单开始播放时，建立独立队列上下文。
    public void Replace(IEnumerable<string> ids)
    {
        _items.Clear();
        _known.Clear();
        _requestedNext.Clear();
        _nextAfterRemoval = 0;
        Merge(ids);
    }

    // 自动结束遵循单曲循环；手动上下首始终允许切歌。随机模式避免立即重复当前曲。
    public string? Next(QueuePlaybackMode mode, int direction = 1, bool automatic = false)
    {
        if (_items.Count == 0) return null;
        // 用户明确指定的下一首优先于随机和单曲循环，开始播放后才消费请求。
        if (direction > 0 && _requestedNext.Count > 0) return _requestedNext[0];
        if (automatic && mode == QueuePlaybackMode.RepeatOne && IndexOf(_currentId) >= 0) return _currentId;
        if (mode != QueuePlaybackMode.Shuffle || _items.Count == 1) return Adjacent(direction);
        var candidates = _items.Where(id => !string.Equals(id, _currentId, StringComparison.OrdinalIgnoreCase)).ToArray();
        return candidates[Random.Shared.Next(candidates.Length)];
    }

    public void Merge(IEnumerable<string> ids)
    {
        foreach (var id in ids) if (_known.Add(id)) _items.Add(id);
    }

    public void Select(string id)
    {
        _requestedNext.RemoveAll(item => string.Equals(item, id, StringComparison.OrdinalIgnoreCase));
        _known.Add(id);
        if (IndexOf(id) < 0) _items.Add(id);
        _currentId = id;
    }

    // 当前曲目移出队列后继续播放，下一首从原位置向后接续。
    public void Remove(string id)
    {
        _requestedNext.RemoveAll(item => string.Equals(item, id, StringComparison.OrdinalIgnoreCase));
        var index = IndexOf(id);
        if (index < 0) return;
        if (IndexOf(_currentId) < 0 && index < _nextAfterRemoval) _nextAfterRemoval--;
        if (string.Equals(id, _currentId, StringComparison.OrdinalIgnoreCase)) _nextAfterRemoval = index;
        _items.RemoveAt(index);
    }

    public void AddNext(string id)
    {
        if (string.Equals(id, _currentId, StringComparison.OrdinalIgnoreCase) && IndexOf(id) >= 0) return;
        Remove(id);
        var current = IndexOf(_currentId);
        var index = current >= 0 ? current + 1 : Math.Clamp(_nextAfterRemoval, 0, _items.Count);
        _items.Insert(index, id);
        _known.Add(id);
        _requestedNext.Insert(0, id);
    }

    public string? Adjacent(int direction)
    {
        if (_items.Count == 0) return null;
        var current = IndexOf(_currentId);
        var index = current >= 0 ? current + direction : _nextAfterRemoval + (direction < 0 ? -1 : 0);
        return _items[(index % _items.Count + _items.Count) % _items.Count];
    }

    private int IndexOf(string? id) => _items.FindIndex(item => string.Equals(item, id, StringComparison.OrdinalIgnoreCase));
}
