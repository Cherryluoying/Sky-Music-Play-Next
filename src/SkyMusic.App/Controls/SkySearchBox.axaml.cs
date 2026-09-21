// 模块：SkyMusic.App 自定义控件 SkySearchBox.axaml
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace SkyMusic.App.Controls;

public sealed partial class SkySearchBox : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SkySearchBox, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<SkySearchBox, string>(nameof(Watermark), string.Empty);

    public SkySearchBox() => InitializeComponent();

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }
}
