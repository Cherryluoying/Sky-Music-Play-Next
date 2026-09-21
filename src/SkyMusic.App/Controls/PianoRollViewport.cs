// 模块：SkyMusic.App 自定义控件 PianoRollViewport
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SkyMusic.App.Controls;

public sealed class PianoRollViewport : Control
{
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#111A29"));
    private static readonly IBrush KeyboardBrush = new SolidColorBrush(Color.Parse("#F4F7FB"));
    private static readonly IBrush BlackKeyBrush = new SolidColorBrush(Color.Parse("#263349"));
    private static readonly IPen BeatPen = new Pen(new SolidColorBrush(Color.Parse("#243247")), 1);
    private static readonly IPen BarPen = new Pen(new SolidColorBrush(Color.Parse("#3B4C66")), 1);
    private static readonly IBrush[] NoteBrushes =
    [
        new SolidColorBrush(Color.Parse("#3B82F6")),
        new SolidColorBrush(Color.Parse("#8B5CF6")),
        new SolidColorBrush(Color.Parse("#06B6D4"))
    ];

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        context.FillRectangle(BackgroundBrush, bounds);
        const double keyboardWidth = 74;
        const double rowHeight = 18;
        const double beatWidth = 72;

        for (var row = 0; row < Math.Ceiling(bounds.Height / rowHeight); row++)
        {
            var y = row * rowHeight;
            var isBlack = row % 12 is 1 or 3 or 6 or 8 or 10;
            context.FillRectangle(isBlack ? BlackKeyBrush : KeyboardBrush,
                new Rect(0, y, keyboardWidth, rowHeight - 1));
            context.DrawLine(BeatPen, new Point(keyboardWidth, y), new Point(bounds.Width, y));
        }

        for (var beat = 0; keyboardWidth + (beat * beatWidth) < bounds.Width; beat++)
        {
            var x = keyboardWidth + (beat * beatWidth);
            context.DrawLine(beat % 4 == 0 ? BarPen : BeatPen, new Point(x, 0), new Point(x, bounds.Height));
        }

        var notes = new[]
        {
            new Rect(keyboardWidth + 38, 72, 106, 14),
            new Rect(keyboardWidth + 152, 108, 70, 14),
            new Rect(keyboardWidth + 230, 54, 142, 14),
            new Rect(keyboardWidth + 382, 126, 86, 14),
            new Rect(keyboardWidth + 476, 90, 118, 14),
            new Rect(keyboardWidth + 602, 144, 74, 14)
        };
        for (var index = 0; index < notes.Length; index++)
            context.FillRectangle(NoteBrushes[index % NoteBrushes.Length], notes[index], 3);
    }
}
