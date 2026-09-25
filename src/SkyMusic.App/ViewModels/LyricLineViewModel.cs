// 模块：SkyMusic.App 界面状态 LyricLineViewModel
using Avalonia.Media;
using SkyMusic.Core.Models;

namespace SkyMusic.App.ViewModels;

public sealed class LyricLineViewModel(LyricLine line) : ObservableObject
{
    private static readonly IBrush LyricBrush = new SolidColorBrush(Colors.White);
    private int _distance = 4;

    public LyricLine Line { get; } = line;

    public string Text => string.IsNullOrWhiteSpace(Line.Text) ? "♪" : Line.Text;

    public string TimestampText => Line.Timestamp.ToString(@"mm\:ss");

    public bool IsCurrent => Distance == 0;

    public double Opacity => Distance switch
    {
        0 => 1,
        1 => 0.58,
        2 => 0.45,
        _ => 0.38
    };

    // 字号和字重不参与逐句动画，以视觉缩放强调当前句，避免测量宽度改变挤动封面列。
    public double FontSize => 28;

    public double VisualScale => Distance switch
    {
        0 => 1,
        _ => 1 / 1.16
    };

    public IBrush Foreground => LyricBrush;

    public FontWeight LineFontWeight => FontWeight.SemiBold;

    public int Distance
    {
        get => _distance;
        set
        {
            if (!SetProperty(ref _distance, Math.Max(0, value)))
            {
                return;
            }

            OnPropertyChanged(nameof(IsCurrent));
            OnPropertyChanged(nameof(Opacity));
            OnPropertyChanged(nameof(VisualScale));
        }
    }
}
