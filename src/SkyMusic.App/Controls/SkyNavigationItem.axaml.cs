// 模块：SkyMusic.App 自定义控件 SkyNavigationItem.axaml
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace SkyMusic.App.Controls;

public sealed partial class SkyNavigationItem : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SkyNavigationItem, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> GlyphProperty =
        AvaloniaProperty.Register<SkyNavigationItem, string>(nameof(Glyph), string.Empty);

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<SkyNavigationItem, bool>(nameof(IsSelected));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<SkyNavigationItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<SkyNavigationItem, object?>(nameof(CommandParameter));

    public SkyNavigationItem() => InitializeComponent();

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

}
