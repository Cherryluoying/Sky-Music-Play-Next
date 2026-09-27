// 模块：SkyMusic.App 界面状态 LyricLineViewModel
using Avalonia.Media;
using SkyMusic.Core.Models;

namespace SkyMusic.App.ViewModels;

public sealed class LyricLineViewModel(LyricLine line) : ObservableObject
{
    private static readonly IBrush LyricBrush = new SolidColorBrush(Colors.White);
    private int _distance = 4;
    private LyricsAppearance? _appearance;

    public LyricLine Line { get; } = line;

    public string Text => string.IsNullOrWhiteSpace(Line.Text) ? "♪" : Line.Text;
    public string Translation => Line.Translation ?? string.Empty;
    public bool HasTranslation => !string.IsNullOrWhiteSpace(Translation);

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
    public double FontSize => _appearance?.FontSize ?? 28;
    public double TranslationFontSize => FontSize * (_appearance?.TranslationSizePercent ?? 80) / 100;
    // 译文非当前句只轻微缩小，避免双重缩放后难以辨认；布局始终保留完整字号空间。
    public double TranslationVisualScale => IsCurrent ? 1 : .88;
    public double LetterSpacing => _appearance?.LetterSpacing ?? 0;
    public double TranslationSpacing => _appearance?.TranslationSpacing ?? 6;
    public Avalonia.Thickness LinePadding => new(12, (_appearance?.LineSpacing ?? 24) / 2, 8, (_appearance?.LineSpacing ?? 24) / 2);

    public double VisualScale => Distance switch
    {
        0 => 1,
        _ => .72
    };

    public IBrush Foreground => LyricBrush;

    public FontWeight LineFontWeight => LyricsAppearance.Weight(IsCurrent ? _appearance?.CurrentWeightIndex ?? 3 : _appearance?.OtherWeightIndex ?? 0);

    // 每次排版修改统一更新；不重建歌词数据，不打断正在播放的句子。
    public void ApplyAppearance(LyricsAppearance appearance)
    {
        _appearance = appearance;
        foreach (var property in new[] { nameof(FontSize), nameof(TranslationFontSize), nameof(LetterSpacing),
            nameof(TranslationSpacing), nameof(LinePadding), nameof(LineFontWeight) }) OnPropertyChanged(property);
    }

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
            OnPropertyChanged(nameof(TranslationVisualScale));
            OnPropertyChanged(nameof(LineFontWeight));
        }
    }
}
