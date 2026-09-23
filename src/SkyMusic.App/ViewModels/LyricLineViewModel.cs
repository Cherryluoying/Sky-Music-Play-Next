// 模块：SkyMusic.App 界面状态 LyricLineViewModel
using Avalonia.Media;
using SkyMusic.Core.Models;

namespace SkyMusic.App.ViewModels;

public sealed class LyricLineViewModel(LyricLine line) : ObservableObject
{
    private static readonly IBrush LyricBrush = new SolidColorBrush(Colors.White);
    private int _distance = 4;

    public LyricLine Line { get; } = line;

    public string Text => Line.Text;

    public string TimestampText => Line.Timestamp.ToString(@"mm\:ss");

    public bool IsCurrent => Distance == 0;

    public double Opacity => Distance switch
    {
        0 => 1,
        1 => 0.64,
        2 => 0.42,
        _ => 0.24
    };

    public double FontSize => Distance switch
    {
        0 => 28,
        1 => 19,
        2 => 17,
        _ => 16
    };

    public IBrush Foreground => LyricBrush;

    public FontWeight LineFontWeight => IsCurrent ? FontWeight.Bold : FontWeight.SemiBold;

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
            OnPropertyChanged(nameof(FontSize));
            OnPropertyChanged(nameof(LineFontWeight));
        }
    }
}
