// 模块：SkyMusic.App MIDI 钢琴窗可视化控件
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SkyMusic.Core.Models;

namespace SkyMusic.App.Controls;

public sealed class MidiPianoRoll : Control
{
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

    // 以当前播放位置为基准绘制未来 5 秒的下落音符和底部 88 键钢琴。
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        context.FillRectangle(new SolidColorBrush(Color.Parse("#7A071220")), bounds, 12);
        const double keyboardHeight = 72;
        var rollHeight = Math.Max(1, bounds.Height - keyboardHeight);
        var keyWidth = bounds.Width / 88d;

        for (var note = 21; note <= 108; note++)
        {
            var x = (note - 21) * keyWidth;
            var black = note % 12 is 1 or 3 or 6 or 8 or 10;
            if (!black)
            {
                context.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#18FFFFFF")), 1),
                    new Point(x, 0), new Point(x, rollHeight));
            }
        }

        var now = PositionSeconds;
        foreach (var note in Notes ?? [])
        {
            var start = note.Start.TotalSeconds;
            var end = start + note.Duration.TotalSeconds;
            if (end < now - 0.15 || start > now + 5)
            {
                continue;
            }

            var x = (Math.Clamp(note.Note, 21, 108) - 21) * keyWidth + 1;
            var y = rollHeight - ((start - now) / 5d * rollHeight);
            var height = Math.Max(5, note.Duration.TotalSeconds / 5d * rollHeight);
            var active = start <= now && end >= now;
            var brush = new SolidColorBrush(Color.Parse(active ? "#78C7FF" : "#3E9BEE"));
            context.FillRectangle(brush, new Rect(x, y - height, Math.Max(2, keyWidth - 2), height), 3);
        }

        // 白键在底层，黑键后绘制，保持真实钢琴键盘层级。
        for (var note = 21; note <= 108; note++)
        {
            var black = note % 12 is 1 or 3 or 6 or 8 or 10;
            if (black)
            {
                continue;
            }
            var x = (note - 21) * keyWidth;
            context.FillRectangle(Brushes.White, new Rect(x, rollHeight, keyWidth, keyboardHeight));
            context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#354A6075")), 1),
                new Rect(x, rollHeight, keyWidth, keyboardHeight));
        }
        for (var note = 21; note <= 108; note++)
        {
            if (note % 12 is not (1 or 3 or 6 or 8 or 10))
            {
                continue;
            }
            var x = (note - 21) * keyWidth - keyWidth * 0.32;
            context.FillRectangle(new SolidColorBrush(Color.Parse("#111923")),
                new Rect(x, rollHeight, keyWidth * 0.64, keyboardHeight * 0.62), 2);
        }
    }
}
