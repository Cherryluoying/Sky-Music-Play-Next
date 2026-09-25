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

    public static readonly DirectProperty<SkySearchBox, string> EffectivePlaceholderProperty =
        AvaloniaProperty.RegisterDirect<SkySearchBox, string>(nameof(EffectivePlaceholder), control => control.EffectivePlaceholder);
    private string _effectivePlaceholder = string.Empty;
    public string EffectivePlaceholder => _effectivePlaceholder;

    public SkySearchBox() => InitializeComponent();

    // 继续使用 TextBox 原生占位符；非空时清空占位内容，防止状态切换中出现双层文字。
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == WatermarkProperty)
            SetAndRaise(EffectivePlaceholderProperty, ref _effectivePlaceholder,
                string.IsNullOrEmpty(Text) ? Watermark : string.Empty);
    }

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
