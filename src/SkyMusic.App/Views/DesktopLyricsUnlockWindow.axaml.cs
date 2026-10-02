// 模块：常驻的小型解锁入口；不激活窗口，不拦截歌词正文区域的鼠标事件。
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia;

namespace SkyMusic.App.Views;

public sealed partial class DesktopLyricsUnlockWindow : Window
{
    public event EventHandler? UnlockRequested;
    public DesktopLyricsUnlockWindow()
    {
        InitializeComponent();
        if (OperatingSystem.IsWindows()) Win32Properties.AddWndProcHookCallback(this, WindowMessage);
        Closed += (_, _) =>
        {
            if (OperatingSystem.IsWindows()) Win32Properties.RemoveWndProcHookCallback(this, WindowMessage);
        };
    }

    private void Unlock_OnClick(object? sender, RoutedEventArgs e) => UnlockRequested?.Invoke(this, EventArgs.Empty);
    private nint WindowMessage(nint hwnd, uint message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0021) { handled = true; return 3; } // MA_NOACTIVATE
        return 0;
    }
}
