// 模块：SkyMusic.Core 游戏乐谱领域 GameScoreEditor
namespace SkyMusic.Core.GameScores;

public sealed class GameScoreEditor
{
    // 切换指定列与图层中的游戏按键音符
    public GameScoreDocument ToggleNote(GameScoreDocument document, int columnIndex, int keyIndex, int layer)
    {
        ValidatePosition(document, columnIndex, keyIndex, layer);
        var columns = document.Columns.ToArray();
        var column = columns[columnIndex];
        var notes = column.Notes.ToList();
        var noteIndex = notes.FindIndex(note => note.KeyIndex == keyIndex);
        var mask = 1UL << layer;

        if (noteIndex < 0)
        {
            notes.Add(new GameScoreNote(keyIndex, mask));
        }
        else
        {
            var updatedMask = notes[noteIndex].LayerMask ^ mask;
            if (updatedMask == 0)
                notes.RemoveAt(noteIndex);
            else
                notes[noteIndex] = notes[noteIndex] with { LayerMask = updatedMask };
        }

        columns[columnIndex] = column with { Notes = notes.OrderBy(note => note.KeyIndex).ToArray() };
        return document with { Columns = columns };
    }

    public GameScoreDocument AddColumns(GameScoreDocument document, int afterIndex, int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var columns = document.Columns.ToList();
        var insertAt = Math.Clamp(afterIndex + 1, 0, columns.Count);
        columns.InsertRange(insertAt, Enumerable.Range(0, count).Select(_ => GameScoreColumn.Empty));
        var breakpoints = document.Breakpoints.Select(value => value >= insertAt ? value + count : value).ToArray();
        return document with { Columns = columns, Breakpoints = breakpoints };
    }

    public GameScoreDocument DeleteColumns(GameScoreDocument document, IEnumerable<int> indexes)
    {
        var removed = indexes.Where(index => index >= 0 && index < document.Columns.Count).Distinct().Order().ToArray();
        if (removed.Length == 0)
            return document;

        var removedSet = removed.ToHashSet();
        var columns = document.Columns.Where((_, index) => !removedSet.Contains(index)).ToList();
        if (columns.Count == 0)
            columns.AddRange(Enumerable.Range(0, 12).Select(_ => GameScoreColumn.Empty));

        var breakpoints = document.Breakpoints
            .Where(value => !removedSet.Contains(value))
            .Select(value => value - removed.Count(removedIndex => removedIndex < value))
            .Where(value => value >= 0 && value < columns.Count)
            .Distinct()
            .Order()
            .ToList();
        if (breakpoints.Count == 0)
            breakpoints.Add(0);
        return document with { Columns = columns, Breakpoints = breakpoints };
    }

    public IReadOnlyList<GameScoreColumn> CopyColumns(GameScoreDocument document, IEnumerable<int> indexes, int? layer = null)
    {
        return indexes
            .Where(index => index >= 0 && index < document.Columns.Count)
            .Distinct()
            .Order()
            .Select(index => CopyColumn(document.Columns[index], layer))
            .ToArray();
    }

    // 按覆盖或合并模式粘贴复制的谱面列
    public GameScoreDocument PasteColumns(
        GameScoreDocument document,
        int atIndex,
        IReadOnlyList<GameScoreColumn> copied,
        bool merge)
    {
        if (copied.Count == 0)
            return document;
        var columns = document.Columns.ToList();
        var target = Math.Clamp(atIndex, 0, columns.Count);
        if (!merge)
        {
            columns.InsertRange(target, copied.Select(CloneColumn));
            var breakpoints = document.Breakpoints
                .Select(value => value >= target ? value + copied.Count : value)
                .ToArray();
            return document with { Columns = columns, Breakpoints = breakpoints };
        }

        for (var offset = 0; offset < copied.Count && target + offset < columns.Count; offset++)
            columns[target + offset] = MergeColumn(columns[target + offset], copied[offset]);
        return document with { Columns = columns };
    }

    public GameScoreDocument Erase(GameScoreDocument document, IEnumerable<int> indexes, int? layer = null)
    {
        var columns = document.Columns.ToArray();
        foreach (var index in indexes.Where(index => index >= 0 && index < columns.Length).Distinct())
        {
            if (layer is null)
            {
                columns[index] = columns[index] with { Notes = [] };
                continue;
            }

            var mask = ~(1UL << layer.Value);
            columns[index] = columns[index] with
            {
                Notes = columns[index].Notes
                    .Select(note => note with { LayerMask = note.LayerMask & mask })
                    .Where(note => note.LayerMask != 0)
                    .ToArray()
            };
        }
        return document with { Columns = columns };
    }

    public GameScoreDocument MoveNotes(
        GameScoreDocument document,
        IEnumerable<int> indexes,
        int amount,
        int? layer = null)
    {
        var columns = document.Columns.ToArray();
        foreach (var index in indexes.Where(index => index >= 0 && index < columns.Length).Distinct())
            columns[index] = MoveColumn(columns[index], amount, document.KeyCount, layer);
        return document with { Columns = columns };
    }

    // 调整乐器图层顺序并同步所有列的位掩码
    public GameScoreDocument MoveLayer(GameScoreDocument document, int from, int to, bool swap)
    {
        if (from < 0 || from >= document.Instruments.Count || to < 0 || to >= document.Instruments.Count)
            throw new ArgumentOutOfRangeException(nameof(from));
        if (from == to)
            return document;

        var instruments = document.Instruments.ToArray();
        if (swap)
            (instruments[from], instruments[to]) = (instruments[to], instruments[from]);
        else
        {
            var item = instruments[from];
            var list = instruments.ToList();
            list.RemoveAt(from);
            list.Insert(to, item);
            instruments = list.ToArray();
        }

        var columns = document.Columns.Select(column => column with
        {
            Notes = column.Notes.Select(note => note with
            {
                LayerMask = RemapLayerMask(note.LayerMask, from, to, swap, document.Instruments.Count)
            }).ToArray()
        }).ToArray();
        return document with { Instruments = instruments, Columns = columns };
    }

    public GameScoreDocument AddInstrument(GameScoreDocument document, string name)
    {
        if (document.Instruments.Count >= GameScoreDocument.MaxLayers)
            throw new InvalidOperationException("Maximum game score layer count reached");
        var icons = new[] { GameNoteIcon.Border, GameNoteIcon.Circle, GameNoteIcon.Line };
        var layer = new GameInstrumentLayer(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(name) ? $"Layer {document.Instruments.Count + 1}" : name,
            Icon: icons[document.Instruments.Count % icons.Length]);
        return document with { Instruments = document.Instruments.Append(layer).ToArray() };
    }

    public GameScoreDocument UpdateInstrument(
        GameScoreDocument document,
        int layer,
        Func<GameInstrumentLayer, GameInstrumentLayer> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (layer < 0 || layer >= document.Instruments.Count)
            throw new ArgumentOutOfRangeException(nameof(layer));
        var instruments = document.Instruments.ToArray();
        instruments[layer] = update(instruments[layer]);
        return document with { Instruments = instruments };
    }

    public GameScoreDocument RemoveInstrument(GameScoreDocument document, int layer)
    {
        if (document.Instruments.Count == 1)
            throw new InvalidOperationException("A game score must keep at least one layer");
        if (layer < 0 || layer >= document.Instruments.Count)
            throw new ArgumentOutOfRangeException(nameof(layer));

        var instruments = document.Instruments.Where((_, index) => index != layer).ToArray();
        var lowerMask = (1UL << layer) - 1;
        var columns = document.Columns.Select(column => column with
        {
            Notes = column.Notes.Select(note =>
            {
                var lower = note.LayerMask & lowerMask;
                var upper = (note.LayerMask >> (layer + 1)) << layer;
                return note with { LayerMask = lower | upper };
            }).Where(note => note.LayerMask != 0).ToArray()
        }).ToArray();
        return document with { Instruments = instruments, Columns = columns };
    }

    public GameScoreDocument SetTempoStep(GameScoreDocument document, int columnIndex, int tempoStep)
    {
        if (columnIndex < 0 || columnIndex >= document.Columns.Count)
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        GameTempoSteps.GetRatio(tempoStep);
        var columns = document.Columns.ToArray();
        columns[columnIndex] = columns[columnIndex] with { TempoStep = tempoStep };
        return document with { Columns = columns };
    }

    public GameScoreDocument ToggleBreakpoint(GameScoreDocument document, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= document.Columns.Count)
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        var breakpoints = document.Breakpoints.ToList();
        if (!breakpoints.Remove(columnIndex))
            breakpoints.Add(columnIndex);
        if (breakpoints.Count == 0)
            breakpoints.Add(0);
        return document with { Breakpoints = breakpoints.Distinct().Order().ToArray() };
    }

    private static void ValidatePosition(GameScoreDocument document, int column, int key, int layer)
    {
        if (column < 0 || column >= document.Columns.Count)
            throw new ArgumentOutOfRangeException(nameof(column));
        if (key < 0 || key >= document.KeyCount)
            throw new ArgumentOutOfRangeException(nameof(key));
        if (layer < 0 || layer >= document.Instruments.Count)
            throw new ArgumentOutOfRangeException(nameof(layer));
    }

    private static GameScoreColumn CopyColumn(GameScoreColumn source, int? layer)
    {
        if (layer is null)
            return CloneColumn(source);
        var mask = 1UL << layer.Value;
        return source with
        {
            Notes = source.Notes
                .Where(note => (note.LayerMask & mask) != 0)
                .Select(note => note with { LayerMask = mask })
                .ToArray()
        };
    }

    private static GameScoreColumn CloneColumn(GameScoreColumn source)
        => source with { Notes = source.Notes.ToArray() };

    private static GameScoreColumn MergeColumn(GameScoreColumn target, GameScoreColumn source)
    {
        var notes = target.Notes.ToDictionary(note => note.KeyIndex);
        foreach (var note in source.Notes)
            notes[note.KeyIndex] = notes.TryGetValue(note.KeyIndex, out var current)
                ? current with { LayerMask = current.LayerMask | note.LayerMask }
                : note;
        return target with { Notes = notes.Values.OrderBy(note => note.KeyIndex).ToArray() };
    }

    private static GameScoreColumn MoveColumn(GameScoreColumn source, int amount, int keyCount, int? layer)
    {
        var result = new Dictionary<int, ulong>();
        foreach (var note in source.Notes)
        {
            var movingMask = layer is null ? note.LayerMask : note.LayerMask & (1UL << layer.Value);
            var staticMask = note.LayerMask & ~movingMask;
            if (staticMask != 0)
                result[note.KeyIndex] = result.GetValueOrDefault(note.KeyIndex) | staticMask;
            var target = note.KeyIndex + amount;
            if (movingMask != 0 && target >= 0 && target < keyCount)
                result[target] = result.GetValueOrDefault(target) | movingMask;
        }
        return source with
        {
            Notes = result.OrderBy(pair => pair.Key).Select(pair => new GameScoreNote(pair.Key, pair.Value)).ToArray()
        };
    }

    private static ulong RemapLayerMask(ulong mask, int from, int to, bool swap, int count)
    {
        // 图层排序必须同步搬移每个音符的位掩码
        var bits = Enumerable.Range(0, count).Select(index => (mask & (1UL << index)) != 0).ToList();
        if (swap)
            (bits[from], bits[to]) = (bits[to], bits[from]);
        else
        {
            var bit = bits[from];
            bits.RemoveAt(from);
            bits.Insert(to, bit);
        }
        ulong result = 0;
        for (var index = 0; index < bits.Count; index++)
            if (bits[index])
                result |= 1UL << index;
        return result;
    }
}
