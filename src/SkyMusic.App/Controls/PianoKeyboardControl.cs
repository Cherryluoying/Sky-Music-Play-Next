// 模块：SkyMusic.App 自定义控件 PianoKeyboardControl
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SkyMusic.App.Controls;

public sealed class PianoKeyboardControl : Control
{
    private static readonly IBrush WhiteKeyBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush BlackKeyBrush = new SolidColorBrush(Color.Parse("#263142"));
    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#1473E6"));
    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.Parse("#C9D1DC")), 1);
    private IReadOnlySet<int> _activeNotes = new HashSet<int>();

    public void SetActiveNotes(IReadOnlySet<int> notes)
    {
        _activeNotes = new HashSet<int>(notes);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var whiteNotes = Enumerable.Range(21, 88).Where(note => !IsBlack(note)).ToArray();
        var whiteWidth = Bounds.Width / whiteNotes.Length;
        var whitePositions = new Dictionary<int, double>();

        for (var index = 0; index < whiteNotes.Length; index++)
        {
            var note = whiteNotes[index];
            var x = index * whiteWidth;
            whitePositions[note] = x;
            context.DrawRectangle(
                _activeNotes.Contains(note) ? ActiveBrush : WhiteKeyBrush,
                BorderPen,
                new Rect(x, 0, whiteWidth + 0.5, Bounds.Height));
        }

        var blackWidth = whiteWidth * 0.62;
        var blackHeight = Bounds.Height * 0.62;
        foreach (var note in Enumerable.Range(21, 88).Where(IsBlack))
        {
            var previousWhite = note - 1;
            while (previousWhite >= 21 && IsBlack(previousWhite))
            {
                previousWhite--;
            }

            if (!whitePositions.TryGetValue(previousWhite, out var previousX))
            {
                continue;
            }

            var x = previousX + whiteWidth - (blackWidth / 2);
            context.DrawRectangle(
                _activeNotes.Contains(note) ? ActiveBrush : BlackKeyBrush,
                null,
                new Rect(x, 0, blackWidth, blackHeight),
                2,
                2);
        }
    }

    private static bool IsBlack(int note) => note % 12 is 1 or 3 or 6 or 8 or 10;
}
