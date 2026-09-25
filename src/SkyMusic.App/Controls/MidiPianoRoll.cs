// 模块：SkyMusic.App MIDI 钢琴窗可视化控件
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SkyMusic.Core.Models;

namespace SkyMusic.App.Controls;

public sealed class MidiPianoRoll : Control
{
    // 缓存绘图资源和时间索引；60 FPS 时不为每个音符重复解析颜色、创建画刷。
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#CC071220"));
    private static readonly IBrush NoteBrush = new SolidColorBrush(Color.Parse("#FF63A9F5"));
    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#FFFFCB70"));
    private static readonly IBrush WhiteBrush = new SolidColorBrush(Color.Parse("#FFF7FAFC"));
    private static readonly IBrush BlackBrush = new SolidColorBrush(Color.Parse("#FF111923"));
    private static readonly IBrush BlackActiveBrush = new SolidColorBrush(Color.Parse("#FFE5A64F"));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#22FFFFFF")), 1);
    private static readonly Pen KeyGridPen = new(new SolidColorBrush(Color.Parse("#18FFFFFF")), 1);
    private static readonly Pen KeyBorderPen = new(new SolidColorBrush(Color.Parse("#354A6075")), 1);
    private static readonly Pen ActivePen = new(new SolidColorBrush(Color.Parse("#FFFFF4D6")), 1.5);
    private static readonly Pen StrikePen = new(new SolidColorBrush(Color.Parse("#D9FFCB70")), 2);
    private readonly bool[] _activeKeys = new bool[128];
    private MidiVisualNote[] _sortedNotes = [];
    private double[] _latestEnds = [];

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != NotesProperty) return;
        _sortedNotes = (Notes ?? []).OrderBy(n => n.Start).ToArray();
        _latestEnds = new double[_sortedNotes.Length];
        double end = 0;
        for (var i = 0; i < _sortedNotes.Length; i++)
            _latestEnds[i] = end = Math.Max(end, (_sortedNotes[i].Start + _sortedNotes[i].Duration).TotalSeconds);
    }

    // 前缀最大结束时间用于跳过已经播放的音符，同时不会漏掉跨越当前时间的长音。
    private int FirstVisibleNote(double now)
    {
        var low = 0;
        var high = _latestEnds.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_latestEnds[middle] <= now) low = middle + 1;
            else high = middle;
        }
        return low;
    }
    public static readonly StyledProperty<IReadOnlyList<MidiVisualNote>?> NotesProperty =
        AvaloniaProperty.Register<MidiPianoRoll, IReadOnlyList<MidiVisualNote>?>(nameof(Notes));

    public static readonly StyledProperty<double> PositionSecondsProperty =
        AvaloniaProperty.Register<MidiPianoRoll, double>(nameof(PositionSeconds));

    static MidiPianoRoll()
    {
        AffectsRender<MidiPianoRoll>(NotesProperty, PositionSecondsProperty);
    }

    public IReadOnlyList<MidiVisualNote>? Notes
    {
        get => GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    public double PositionSeconds
    {
        get => GetValue(PositionSecondsProperty);
        set => SetValue(PositionSecondsProperty, value);
    }

    // 以当前播放位置为基准绘制未来八秒音符和底部 88 键钢琴。
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        context.FillRectangle(BackgroundBrush, new Rect(bounds.Size), 12);
        const double keyboardHeight = 78;
        const double lookAheadSeconds = 8d;
        var rollHeight = Math.Max(1, bounds.Height - keyboardHeight);
        // A0 到 C8 共 52 个白键，黑键位于相邻白键边界。
        const int whiteKeyCount = 52;
        var keyWidth = bounds.Width / whiteKeyCount;

        // 背景节拍线让音符运动方向更容易识别，保持低对比度。
        for (var step = 1; step < 8; step++)
        {
            var y = rollHeight - rollHeight * step / 8d;
            context.DrawLine(GridPen,
                new Point(0, y), new Point(bounds.Width, y));
        }

        for (var note = 21; note <= 108; note++)
        {
            var x = GetKeyLeft(note, keyWidth);
            if (!IsBlackKey(note))
            {
                context.DrawLine(KeyGridPen,
                    new Point(x, 0), new Point(x, rollHeight));
            }
        }

        var now = Math.Max(0, PositionSeconds);
        Array.Clear(_activeKeys);
        for (var i = FirstVisibleNote(now); i < _sortedNotes.Length; i++)
        {
            var note = _sortedNotes[i];
            var start = note.Start.TotalSeconds;
            if (start > now + lookAheadSeconds) break;
            // 88 键范围以外的 MIDI 保留播放，但不能伪装成最左或最右的钢琴键。
            if (note.Note is < 21 or > 108) continue;
            var end = start + note.Duration.TotalSeconds;
            if (start <= now && end > now)
            {
                _activeKeys[note.Note] = true;
            }
            if (end < now - 0.2 || start > now + lookAheadSeconds)
            {
                continue;
            }

            var noteNumber = note.Note;
            var noteWidth = IsBlackKey(noteNumber) ? keyWidth * 0.62 : keyWidth * 0.9;
            var x = GetKeyLeft(noteNumber, keyWidth) + (IsBlackKey(noteNumber) ? 0 : (keyWidth - noteWidth) / 2);
            var y = rollHeight - ((start - now) / lookAheadSeconds * rollHeight);
            var height = Math.Max(2, note.Duration.TotalSeconds / lookAheadSeconds * rollHeight);
            var active = start <= now && end > now;
            // 音符必须裁剪在击键线以上，不能继续覆盖钢琴键盘或底部播放器。
            var bottom = Math.Clamp(y, 0, rollHeight);
            var top = Math.Clamp(y - height, 0, rollHeight);
            if (bottom <= top)
            {
                continue;
            }

            var noteRect = new Rect(x, top, Math.Max(4, noteWidth), bottom - top);
            context.FillRectangle(
                active ? ActiveBrush : NoteBrush,
                noteRect,
                3);
            if (active)
            {
                context.DrawRectangle(ActivePen, noteRect, 3);
            }
        }

        // 当前击键位置使用品牌橘猫色，便于观察音符何时落到键盘。
        context.DrawLine(StrikePen,
            new Point(0, rollHeight - 1), new Point(bounds.Width, rollHeight - 1));

        // 白键在底层，黑键后绘制，保持真实钢琴键盘层级。
        for (var note = 21; note <= 108; note++)
        {
            if (IsBlackKey(note))
            {
                continue;
            }
            var x = GetKeyLeft(note, keyWidth);
            var rect = new Rect(x, rollHeight, keyWidth, keyboardHeight);
            var active = _activeKeys[note];
            context.FillRectangle(active ? ActiveBrush : WhiteBrush, rect);
            context.DrawRectangle(KeyBorderPen, rect);
            if (active)
            {
                context.DrawRectangle(ActivePen, rect, 2);
            }
        }
        for (var note = 21; note <= 108; note++)
        {
            if (!IsBlackKey(note))
            {
                continue;
            }
            var blackWidth = keyWidth * 0.62;
            var x = GetKeyLeft(note, keyWidth);
            var rect = new Rect(x, rollHeight, blackWidth, keyboardHeight * 0.62);
            var active = _activeKeys[note];
            context.FillRectangle(active ? BlackActiveBrush : BlackBrush, rect, 2);
            if (active)
            {
                context.DrawRectangle(ActivePen, rect, 2);
            }
        }
    }

    private static bool IsBlackKey(int note) => note % 12 is 1 or 3 or 6 or 8 or 10;

    private static double GetKeyLeft(int note, double keyWidth)
    {
        var whiteIndex = 0;
        for (var current = 21; current < note; current++)
        {
            if (!IsBlackKey(current))
            {
                whiteIndex++;
            }
        }

        return IsBlackKey(note)
            ? whiteIndex * keyWidth - keyWidth * 0.31
            : whiteIndex * keyWidth;
    }
}
