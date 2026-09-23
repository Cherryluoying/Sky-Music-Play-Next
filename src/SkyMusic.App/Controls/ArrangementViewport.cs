// 模块：SkyMusic.App 专业工作区编排总览
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SkyMusic.Core.Projects;

namespace SkyMusic.App.Controls;

public sealed class ArrangementViewport : Control
{
    public static readonly StyledProperty<IReadOnlyList<ProjectTrack>> TracksProperty =
        AvaloniaProperty.Register<ArrangementViewport, IReadOnlyList<ProjectTrack>>(nameof(Tracks), []);
    public static readonly StyledProperty<Guid?> SelectedTrackIdProperty =
        AvaloniaProperty.Register<ArrangementViewport, Guid?>(nameof(SelectedTrackId));
    public static readonly StyledProperty<int> PpqProperty =
        AvaloniaProperty.Register<ArrangementViewport, int>(nameof(Ppq), MusicProject.DefaultPpq);
    public static readonly StyledProperty<double> PixelsPerQuarterProperty =
        AvaloniaProperty.Register<ArrangementViewport, double>(nameof(PixelsPerQuarter), 96);
    public static readonly StyledProperty<long> PlayheadTickProperty =
        AvaloniaProperty.Register<ArrangementViewport, long>(nameof(PlayheadTick));

    private const double RulerHeight = 28;
    private const double LaneHeight = 46;
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#121920"));
    private static readonly IBrush AlternateBrush = new SolidColorBrush(Color.Parse("#182129"));
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#21323C"));
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#91A0AA"));
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.Parse("#2A3741")), 1);
    private static readonly IPen BarPen = new Pen(new SolidColorBrush(Color.Parse("#4A5B66")), 1);
    private static readonly IPen PlayheadPen = new Pen(new SolidColorBrush(Color.Parse("#EF6A61")), 2);

    static ArrangementViewport() => AffectsRender<ArrangementViewport>(
        TracksProperty, SelectedTrackIdProperty, PpqProperty, PixelsPerQuarterProperty, PlayheadTickProperty);

    public IReadOnlyList<ProjectTrack> Tracks { get => GetValue(TracksProperty); set => SetValue(TracksProperty, value); }
    public Guid? SelectedTrackId { get => GetValue(SelectedTrackIdProperty); set => SetValue(SelectedTrackIdProperty, value); }
    public int Ppq { get => GetValue(PpqProperty); set => SetValue(PpqProperty, value); }
    public double PixelsPerQuarter { get => GetValue(PixelsPerQuarterProperty); set => SetValue(PixelsPerQuarterProperty, value); }
    public long PlayheadTick { get => GetValue(PlayheadTickProperty); set => SetValue(PlayheadTickProperty, value); }

    public event EventHandler<Guid>? TrackSelected;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(BackgroundBrush, Bounds);
        for (var index = 0; index < Tracks.Count; index++)
        {
            var track = Tracks[index];
            var y = RulerHeight + index * LaneHeight;
            context.FillRectangle(track.Id == SelectedTrackId ? SelectedBrush : index % 2 == 1 ? AlternateBrush : BackgroundBrush,
                new Rect(0, y, Bounds.Width, LaneHeight));
            context.DrawLine(GridPen, new Point(0, y + LaneHeight), new Point(Bounds.Width, y + LaneHeight));
            var noteBrush = new SolidColorBrush(Color.Parse(track.Color));
            foreach (var note in track.Notes)
            {
                var x = TickToX(note.StartTick);
                var width = Math.Max(3, note.LengthTicks * PixelsPerQuarter / Math.Max(1, Ppq));
                var pitchOffset = 5 + (127 - note.MidiNote) / 127d * 24;
                context.FillRectangle(noteBrush, new Rect(x, y + pitchOffset, width, 7), 2);
            }
        }

        var maxTick = XToTick(Bounds.Width) + Ppq;
        for (long tick = 0; tick <= maxTick; tick += Ppq)
        {
            var x = TickToX(tick);
            var bar = tick % (Ppq * 4L) == 0;
            context.DrawLine(bar ? BarPen : GridPen, new Point(x, 0), new Point(x, Bounds.Height));
            if (bar)
            {
                var text = new FormattedText($"{tick / (Ppq * 4L) + 1}", System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 10, TextBrush);
                context.DrawText(text, new Point(x + 5, 7));
            }
        }
        var playheadX = TickToX(PlayheadTick);
        context.DrawLine(PlayheadPen, new Point(playheadX, 0), new Point(playheadX, Bounds.Height));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var index = (int)((e.GetPosition(this).Y - RulerHeight) / LaneHeight);
        if (index >= 0 && index < Tracks.Count)
        {
            TrackSelected?.Invoke(this, Tracks[index].Id);
            e.Handled = true;
        }
    }

    private double TickToX(long tick) => tick * PixelsPerQuarter / Math.Max(1, Ppq);
    private long XToTick(double x) => (long)Math.Round(Math.Max(0, x) * Math.Max(1, Ppq) / PixelsPerQuarter);
}
