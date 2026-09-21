// 模块：SkyMusic.App 界面状态 LyricLineViewModel
using Avalonia.Media;
using SkyMusic.Core.Models;

namespace SkyMusic.App.ViewModels;

public sealed class LyricLineViewModel(LyricLine line) : ObservableObject
{
    private bool _isCurrent;

    public LyricLine Line { get; } = line;

    public string Text => Line.Text;

    public double Opacity => IsCurrent ? 1 : 0.46;

    public double FontSize => IsCurrent ? 24 : 16;

    public IBrush Foreground => IsCurrent
        ? new SolidColorBrush(Color.Parse("#126DDA"))
        : new SolidColorBrush(Color.Parse("#20304E"));

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (!SetProperty(ref _isCurrent, value))
            {
                return;
            }

            OnPropertyChanged(nameof(Opacity));
            OnPropertyChanged(nameof(FontSize));
            OnPropertyChanged(nameof(Foreground));
        }
    }
}
