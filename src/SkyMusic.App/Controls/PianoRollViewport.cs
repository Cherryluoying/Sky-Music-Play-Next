// 模块：SkyMusic.App 专业工作区钢琴卷帘
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SkyMusic.App.ViewModels;
using SkyMusic.Core.Projects;

namespace SkyMusic.App.Controls;

public enum PianoRollEditKind { Select, Add, Move, Resize, Delete }

public sealed class PianoRollEditEventArgs(
    PianoRollEditKind kind,
    Guid? noteId = null,
    long startTick = 0,
    long lengthTicks = 0,
    int midiNote = 60) : EventArgs
{
    public PianoRollEditKind Kind { get; } = kind;
    public Guid? NoteId { get; } = noteId;
    public long StartTick { get; } = startTick;
    public long LengthTicks { get; } = lengthTicks;
    public int MidiNote { get; } = midiNote;
}

public sealed class PianoRollViewport : Control
{
    public static readonly StyledProperty<IReadOnlyList<ProjectNote>> NotesProperty =
        AvaloniaProperty.Register<PianoRollViewport, IReadOnlyList<ProjectNote>>(nameof(Notes), []);
    public static readonly StyledProperty<Guid?> SelectedNoteIdProperty =
        AvaloniaProperty.Register<PianoRollViewport, Guid?>(nameof(SelectedNoteId));
    public static readonly StyledProperty<int> PpqProperty =
        AvaloniaProperty.Register<PianoRollViewport, int>(nameof(Ppq), MusicProject.DefaultPpq);
    public static readonly StyledProperty<long> GridTicksProperty =
        AvaloniaProperty.Register<PianoRollViewport, long>(nameof(GridTicks), 120);
    public static readonly StyledProperty<double> PixelsPerQuarterProperty =
        AvaloniaProperty.Register<PianoRollViewport, double>(nameof(PixelsPerQuarter), 96);
    public static readonly StyledProperty<long> PlayheadTickProperty =
        AvaloniaProperty.Register<PianoRollViewport, long>(nameof(PlayheadTick));
    public static readonly StyledProperty<ProfessionalEditTool> ToolProperty =
        AvaloniaProperty.Register<PianoRollViewport, ProfessionalEditTool>(nameof(Tool));
    public static readonly StyledProperty<string> NoteColorProperty =
        AvaloniaProperty.Register<PianoRollViewport, string>(nameof(NoteColor), "#43A6C6");

    private const double KeyboardWidth = 74;
    private const double RowHeight = 18;
    private const int HighestPitch = 108;
    private const int LowestPitch = 21;
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#10151B"));
    private static readonly IBrush AlternateRowBrush = new SolidColorBrush(Color.Parse("#141C24"));
    private static readonly IBrush WhiteKeyBrush = new SolidColorBrush(Color.Parse("#D9DEE3"));
    private static readonly IBrush BlackKeyBrush = new SolidColorBrush(Color.Parse("#39434D"));
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#596774"));
    private static readonly IPen RowPen = new Pen(new SolidColorBrush(Color.Parse("#222D37")), 1);
    private static readonly IPen BeatPen = new Pen(new SolidColorBrush(Color.Parse("#2B3945")), 1);
    private static readonly IPen BarPen = new Pen(new SolidColorBrush(Color.Parse("#50606D")), 1);
    private static readonly IPen SelectionPen = new Pen(new SolidColorBrush(Color.Parse("#F4C95D")), 2);
    private static readonly IPen PlayheadPen = new Pen(new SolidColorBrush(Color.Parse("#EF6A61")), 2);
    private ProjectNote? _dragNote;
    private ProjectNote? _dragPreview;
    private Point _dragOrigin;
    private bool _isResizing;

    static PianoRollViewport() => AffectsRender<PianoRollViewport>(
        NotesProperty, SelectedNoteIdProperty, PpqProperty, GridTicksProperty,
        PixelsPerQuarterProperty, PlayheadTickProperty, ToolProperty, NoteColorProperty);

    public PianoRollViewport()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public IReadOnlyList<ProjectNote> Notes { get => GetValue(NotesProperty); set => SetValue(NotesProperty, value); }
    public Guid? SelectedNoteId { get => GetValue(SelectedNoteIdProperty); set => SetValue(SelectedNoteIdProperty, value); }
    public int Ppq { get => GetValue(PpqProperty); set => SetValue(PpqProperty, value); }
    public long GridTicks { get => GetValue(GridTicksProperty); set => SetValue(GridTicksProperty, value); }
    public double PixelsPerQuarter { get => GetValue(PixelsPerQuarterProperty); set => SetValue(PixelsPerQuarterProperty, value); }
    public long PlayheadTick { get => GetValue(PlayheadTickProperty); set => SetValue(PlayheadTickProperty, value); }
    public ProfessionalEditTool Tool { get => GetValue(ToolProperty); set => SetValue(ToolProperty, value); }
    public string NoteColor { get => GetValue(NoteColorProperty); set => SetValue(NoteColorProperty, value); }

    public event EventHandler<PianoRollEditEventArgs>? EditRequested;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(BackgroundBrush, Bounds);
        DrawRows(context);
        DrawGrid(context);
        foreach (var note in Notes)
            DrawNote(context, note, note.Id == SelectedNoteId);
        if (_dragPreview is not null)
            DrawNote(context, _dragPreview, true, 0.66);

        var playheadX = TickToX(PlayheadTick);
        if (playheadX >= KeyboardWidth && playheadX <= Bounds.Width)
            context.DrawLine(PlayheadPen, new Point(playheadX, 0), new Point(playheadX, Bounds.Height));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetPosition(this);
        if (point.X < KeyboardWidth)
            return;

        var hit = HitTestNote(point);
        var rightClick = e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed;
        if (Tool == ProfessionalEditTool.Erase || rightClick)
        {
            if (hit is not null)
                EditRequested?.Invoke(this, new PianoRollEditEventArgs(PianoRollEditKind.Delete, hit.Id));
            e.Handled = true;
            return;
        }

        if (Tool == ProfessionalEditTool.Draw)
        {
            EditRequested?.Invoke(this, new PianoRollEditEventArgs(
                PianoRollEditKind.Add,
                startTick: QuantizeTick(XToTick(point.X)),
                lengthTicks: Math.Max(1, GridTicks),
                midiNote: YToPitch(point.Y)));
            e.Handled = true;
            return;
        }

        if (hit is null)
        {
            EditRequested?.Invoke(this, new PianoRollEditEventArgs(PianoRollEditKind.Select));
            return;
        }

        EditRequested?.Invoke(this, new PianoRollEditEventArgs(PianoRollEditKind.Select, hit.Id));
        _dragNote = hit;
        _dragPreview = hit;
        _dragOrigin = point;
        _isResizing = Math.Abs(point.X - NoteRect(hit).Right) <= 8;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragNote is null)
            return;
        var point = e.GetPosition(this);
        if (_isResizing)
        {
            var endTick = QuantizeTick(XToTick(point.X));
            _dragPreview = _dragNote with { LengthTicks = Math.Max(GridTicks, endTick - _dragNote.StartTick) };
        }
        else
        {
            var deltaTick = QuantizeTick(XToTick(point.X) - XToTick(_dragOrigin.X));
            var deltaPitch = (int)Math.Round((_dragOrigin.Y - point.Y) / RowHeight);
            _dragPreview = _dragNote with
            {
                StartTick = Math.Max(0, _dragNote.StartTick + deltaTick),
                MidiNote = Math.Clamp(_dragNote.MidiNote + deltaPitch, 0, 127)
            };
        }
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragNote is null || _dragPreview is null)
            return;
        var preview = _dragPreview;
        var original = _dragNote;
        _dragNote = null;
        _dragPreview = null;
        e.Pointer.Capture(null);
        if (_isResizing && preview.LengthTicks != original.LengthTicks)
            EditRequested?.Invoke(this, new PianoRollEditEventArgs(
                PianoRollEditKind.Resize, original.Id, lengthTicks: preview.LengthTicks));
        else if (!_isResizing && (preview.StartTick != original.StartTick || preview.MidiNote != original.MidiNote))
            EditRequested?.Invoke(this, new PianoRollEditEventArgs(
                PianoRollEditKind.Move, original.Id, preview.StartTick, midiNote: preview.MidiNote));
        InvalidateVisual();
        e.Handled = true;
    }

    private void DrawRows(DrawingContext context)
    {
        for (var pitch = HighestPitch; pitch >= LowestPitch; pitch--)
        {
            var y = PitchToY(pitch);
            var black = IsBlackKey(pitch);
            if (black)
                context.FillRectangle(AlternateRowBrush, new Rect(KeyboardWidth, y, Bounds.Width - KeyboardWidth, RowHeight));
            context.FillRectangle(black ? BlackKeyBrush : WhiteKeyBrush, new Rect(0, y, KeyboardWidth, RowHeight - 1));
            context.DrawLine(RowPen, new Point(KeyboardWidth, y), new Point(Bounds.Width, y));
            if (pitch % 12 == 0)
            {
                var text = new FormattedText($"C{pitch / 12 - 1}", System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 10, TextBrush);
                context.DrawText(text, new Point(7, y + 2));
            }
        }
    }

    private void DrawGrid(DrawingContext context)
    {
        var subdivision = Math.Max(1, GridTicks);
        var maxTick = XToTick(Bounds.Width) + subdivision;
        for (long tick = 0; tick <= maxTick; tick += subdivision)
        {
            var x = TickToX(tick);
            var isBeat = tick % Ppq == 0;
            var isBar = tick % (Ppq * 4) == 0;
            context.DrawLine(isBar ? BarPen : isBeat ? BeatPen : RowPen, new Point(x, 0), new Point(x, Bounds.Height));
        }
    }

    private void DrawNote(DrawingContext context, ProjectNote note, bool selected, double opacity = 1)
    {
        if (note.MidiNote is < LowestPitch or > HighestPitch)
            return;
        var color = Color.Parse(NoteColor);
        var brush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B));
        var rect = NoteRect(note).Deflate(new Thickness(1.5, 2));
        context.FillRectangle(brush, rect, 3);
        if (selected)
            context.DrawRectangle(SelectionPen, rect, 3);
    }

    private ProjectNote? HitTestNote(Point point)
        => Notes.Reverse().FirstOrDefault(note => NoteRect(note).Contains(point));

    private Rect NoteRect(ProjectNote note)
        => new(TickToX(note.StartTick), PitchToY(note.MidiNote),
            Math.Max(7, note.LengthTicks * PixelsPerQuarter / Math.Max(1, Ppq)), RowHeight);

    private double TickToX(long tick) => KeyboardWidth + tick * PixelsPerQuarter / Math.Max(1, Ppq);
    private long XToTick(double x) => (long)Math.Round(Math.Max(0, x - KeyboardWidth) * Math.Max(1, Ppq) / PixelsPerQuarter);
    private double PitchToY(int pitch) => (HighestPitch - pitch) * RowHeight;
    private int YToPitch(double y) => Math.Clamp(HighestPitch - (int)(y / RowHeight), LowestPitch, HighestPitch);
    private long QuantizeTick(long tick) => Math.Max(0, (long)Math.Round((decimal)tick / Math.Max(1, GridTicks), MidpointRounding.AwayFromZero) * Math.Max(1, GridTicks));
    private static bool IsBlackKey(int pitch) => pitch % 12 is 1 or 3 or 6 or 8 or 10;
}
