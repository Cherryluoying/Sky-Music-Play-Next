// 模块：SkyMusic.App 页面视图 KeyMappingView.axaml
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SkyMusic.App.Services;
using SkyMusic.App.ViewModels;

namespace SkyMusic.App.Views;

public sealed partial class KeyMappingView : UserControl
{
    private Button? _captureButton;
    private PhysicalKey? _capturedKey;

    public KeyMappingView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, CaptureKey_OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, CaptureKey_OnKeyUp, RoutingStrategies.Tunnel);
        DetachedFromVisualTree += (_, _) => EndCapture();
    }

    private void CaptureKey_OnClick(object? sender, RoutedEventArgs e)
    {
        _capturedKey = null;
        EndCapture();
        if (sender is not Button button) return;
        _captureButton = button;
        button.Content = "按键…";
        button.Focus();
    }

    // 只在显式录入期间拦截，避免空格/回车触发按钮、Tab 移动焦点或输入法产生文字。
    private void CaptureKey_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_capturedKey == e.PhysicalKey) { e.Handled = true; return; }
        if (_captureButton is not { DataContext: KeyMappingEntryViewModel entry }) return;
        e.Handled = true;
        if (e.Key == Key.Escape) { EndCapture(); return; }
        if (KeyboardKeyCatalog.FromPhysicalKey(e.PhysicalKey) is not { } key) return;
        _capturedKey = e.PhysicalKey;
        entry.SelectedKey = key;
        EndCapture();
    }

    private void CaptureKey_OnKeyUp(object? sender, KeyEventArgs e)
    {
        // 空格释放不能再次触发“录入”按钮。
        if (e.PhysicalKey != _capturedKey) return;
        _capturedKey = null;
        e.Handled = true;
    }

    private void CaptureKey_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        _capturedKey = null;
        EndCapture();
    }

    private void EndCapture()
    {
        if (_captureButton is not null) _captureButton.Content = "录入";
        _captureButton = null;
    }
}
