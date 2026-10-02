// 模块：SkyMusic.App 页面视图 PlayerView.axaml
using Avalonia.Controls;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkyMusic.App.ViewModels;
using SkyMusic.App.Services;
using System.ComponentModel;

namespace SkyMusic.App.Views;

public sealed partial class PlayerView : UserControl
{
    public static readonly StyledProperty<bool> IsFullScreenProperty = AvaloniaProperty.Register<PlayerView, bool>(nameof(IsFullScreen));
    public bool IsFullScreen { get => GetValue(IsFullScreenProperty); set => SetValue(IsFullScreenProperty, value); }
    private readonly SurfaceVisibilityMotion _settingsMotion;
    private LyricsAppearance? _appearance;
    public bool HasOpenAppearancePopup => FullScreenSettingsButton.Flyout?.IsOpen == true;
    public bool IsFullScreenSettingsShown => _settingsMotion.IsShown;

    public PlayerView()
    {
        InitializeComponent();
        _settingsMotion = new SurfaceVisibilityMotion(FullScreenSettingsHost, -60);
        DataContextChanged += (_, _) => BindAppearance();
        AttachedToVisualTree += (_, _) => BindAppearance();
        DetachedFromVisualTree += (_, _) =>
        {
            if (_appearance is not null) _appearance.PropertyChanged -= Appearance_OnChanged;
            _appearance = null;
            _settingsMotion.Dispose();
        };
    }

    public void ShowFullScreenSettings(bool visible, bool animate = true)
        => _settingsMotion.SetVisible(IsFullScreen && visible, animate);

    private void BindAppearance()
    {
        if (_appearance is not null) _appearance.PropertyChanged -= Appearance_OnChanged;
        _appearance = (DataContext as PlaybackViewModel)?.LyricsAppearance;
        if (_appearance is not null) _appearance.PropertyChanged += Appearance_OnChanged;
        UpdateLyricsViewport();
    }

    private void Appearance_OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LyricsAppearance.FullScreenPaddingPercent)) UpdateLyricsViewport();
    }

    // 留白只改变右侧歌词的视口；封面与列间距不随设置面板、播放器浮现而移动。
    private void UpdateLyricsViewport()
    {
        if (this.FindControl<Grid>("LyricsPanel") is not { } panel) return;
        var height = LyricsColumns.Bounds.Height;
        var padding = IsFullScreen ? Math.Min(height * (_appearance?.FullScreenPaddingPercent ?? 24) / 100,
            Math.Max(0, (height - 180) / 2)) : 0;
        panel.Margin = new Thickness(0, padding);
    }

    // RowDefinitions 不是 Avalonia 属性；只在模式切换时调整固定预留行，悬停播放器不改变行高。
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsFullScreenProperty) return;
        Classes.Set("fullscreen", IsFullScreen);
        if (this.FindControl<Grid>("PlayerLayout") is not { } layout) return;
        layout.RowDefinitions[0].Height = new GridLength(IsFullScreen ? 0 : 44);
        layout.RowDefinitions[1].Height = new GridLength(IsFullScreen ? 0 : 58);
        layout.RowDefinitions[3].Height = new GridLength(IsFullScreen ? 0 : 88);
        UpdateLyricsViewport();
        if (!IsFullScreen)
        {
            FullScreenSettingsButton.Flyout?.Hide();
            _settingsMotion?.SetVisible(false, false);
        }
    }

    // 列宽只由窗口尺寸决定，长歌词或手动滚动不会重新挤压封面与中间留白。
    private void LyricsColumns_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid || e.NewSize.Width <= 0) return;
        var gap = Math.Clamp(e.NewSize.Width * 0.06, 28, 72);
        var available = Math.Max(0, e.NewSize.Width - gap);
        grid.ColumnDefinitions[0].Width = new GridLength(available * 0.46);
        grid.ColumnDefinitions[1].Width = new GridLength(gap);
        grid.ColumnDefinitions[2].Width = new GridLength(available * 0.54);
        UpdateLyricsViewport();
    }

    // 从沉浸页选择 LRC/TXT，解析与持久化交给播放状态层处理。
    private async void ImportLyrics_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PlaybackViewModel playback || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入本地歌词",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("歌词文件") { Patterns = ["*.lrc", "*.txt"] }
            ]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await playback.ImportLocalLyricsAsync(path);
        }
    }
}
