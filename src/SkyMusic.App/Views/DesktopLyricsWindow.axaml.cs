// 模块：SkyMusic.App 页面视图 DesktopLyricsWindow.axaml
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SkyMusic.App.Views;

public sealed partial class DesktopLyricsWindow : Window
{
    public DesktopLyricsWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var width = (int)(Width * screen.Scaling);
        var height = (int)(Height * screen.Scaling);
        Position = new PixelPoint(
            screen.WorkingArea.X + ((screen.WorkingArea.Width - width) / 2),
            screen.WorkingArea.Bottom - height - 56);
    }

    private void Surface_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            e.Source is Visual visual && visual.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        BeginMoveDrag(e);
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e) => Close();
}
