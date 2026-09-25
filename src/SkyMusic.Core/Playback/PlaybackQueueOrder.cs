// 模块：独立播放队列顺序；曲库刷新只补充新曲目，不复活用户移除的条目。
namespace SkyMusic.Core.Playback;

public sealed class PlaybackQueueOrder
{
    private readonly List<string> _items = [];
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentId;
    private int _nextAfterRemoval;
    public IReadOnlyList<string> Items => _items;

    public void Merge(IEnumerable<string> ids)
    {
        foreach (var id in ids) if (_known.Add(id)) _items.Add(id);
    }

    public void Select(string id)
    {
        _known.Add(id);
        if (IndexOf(id) < 0) _items.Add(id);
        _currentId = id;
    }

    // 当前曲目移出队列后继续播放，下一首从原位置向后接续。
    public void Remove(string id)
    {
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
