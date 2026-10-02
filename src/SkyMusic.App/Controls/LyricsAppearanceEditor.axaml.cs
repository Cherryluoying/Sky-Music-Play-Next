// 模块：歌词外观面板；显示与绑定由 XAML 管理，不保存第二份排版状态。
using Avalonia;
using Avalonia.Controls;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Controls;

public sealed partial class LyricsAppearanceEditor : UserControl
{
    // 字体库独立传入，保留原有外观设置的数据上下文与自动保存行为。
    public static readonly StyledProperty<LyricFontSettings?> FontSettingsProperty =
        AvaloniaProperty.Register<LyricsAppearanceEditor, LyricFontSettings?>(nameof(FontSettings));
    public LyricFontSettings? FontSettings
    {
        get => GetValue(FontSettingsProperty);
        set => SetValue(FontSettingsProperty, value);
    }

    public LyricsAppearanceEditor() => InitializeComponent();
}
