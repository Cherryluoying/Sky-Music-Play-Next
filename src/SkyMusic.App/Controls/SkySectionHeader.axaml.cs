// 模块：SkyMusic.App 自定义控件 SkySectionHeader.axaml
using Avalonia;
using Avalonia.Controls;

namespace SkyMusic.App.Controls;

public sealed partial class SkySectionHeader : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SkySectionHeader, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<SkySectionHeader, string?>(nameof(Hint));

    public SkySectionHeader() => InitializeComponent();

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }
}
