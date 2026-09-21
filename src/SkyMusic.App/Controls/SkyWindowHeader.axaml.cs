// 模块：SkyMusic.App 自定义控件 SkyWindowHeader.axaml
using Avalonia;
using Avalonia.Controls;

namespace SkyMusic.App.Controls;

public sealed partial class SkyWindowHeader : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SkyWindowHeader, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<bool> ShowBackProperty =
        AvaloniaProperty.Register<SkyWindowHeader, bool>(nameof(ShowBack), true);

    public static readonly StyledProperty<bool> ShowForwardProperty =
        AvaloniaProperty.Register<SkyWindowHeader, bool>(nameof(ShowForward));

    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<SkyWindowHeader, object?>(nameof(TrailingContent));

    public SkyWindowHeader() => InitializeComponent();

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowBack
    {
        get => GetValue(ShowBackProperty);
        set => SetValue(ShowBackProperty, value);
    }

    public bool ShowForward
    {
        get => GetValue(ShowForwardProperty);
        set => SetValue(ShowForwardProperty, value);
    }

    public object? TrailingContent
    {
        get => GetValue(TrailingContentProperty);
        set => SetValue(TrailingContentProperty, value);
    }
}
