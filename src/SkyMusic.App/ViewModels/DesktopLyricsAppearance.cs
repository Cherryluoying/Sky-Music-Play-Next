// 模块：桌面歌词外观；独立保存显示偏好，不改变播放状态或全局主题。
using System.Text.Json;
using Avalonia.Media;

namespace SkyMusic.App.ViewModels;

public sealed class DesktopLyricsAppearance : ObservableObject
{
    private readonly string _path;
    private double _fontSize = 36;
    private double _opacityPercent = 100;
    private double _outlineWidth = 2;
    private string? _saveError;

    public DesktopLyricsAppearance(string path)
    {
        _path = path;
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) is { } saved)
            {
                FontSize = saved.FontSize;
                OpacityPercent = saved.OpacityPercent;
                OutlineWidth = saved.OutlineWidth;
                TextColor.Hex = saved.TextColor;
                OutlineColor.Hex = saved.OutlineColor;
                FillColor.Hex = saved.FillColor;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            SaveError = "外观设置读取失败，已使用默认样式。";
        }

        foreach (var color in Colors)
            // 不重置 ItemsSource，否则输入色值时会重建输入框并丢失焦点。
            color.PropertyChanged += (_, _) => OnPropertyChanged("Palette");
    }

    public double FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, Bound(value, 20, 64, 36));
    }

    public double OpacityPercent
    {
        get => _opacityPercent;
        set => SetProperty(ref _opacityPercent, Bound(value, 20, 100, 100));
    }

    public double OutlineWidth
    {
        get => _outlineWidth;
        set => SetProperty(ref _outlineWidth, Bound(value, 0, 4, 2));
    }

    public DesktopLyricColor TextColor { get; } = new("文字颜色 · 未唱部分", "#FFFFFF");
    public DesktopLyricColor OutlineColor { get; } = new("文字描边", "#20242C");
    public DesktopLyricColor FillColor { get; } = new("动画填充 · 已唱部分", "#FFCB70");
    public IEnumerable<DesktopLyricColor> Colors => [TextColor, OutlineColor, FillColor];
    public string? SaveError { get => _saveError; private set => SetProperty(ref _saveError, value); }

    private static double Bound(double value, double min, double max, double fallback)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    // 由窗口防抖调用，原子替换小型配置文件；保存失败不会影响播放或关闭窗口。
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var saved = new Preferences(FontSize, OpacityPercent, OutlineWidth,
                TextColor.ValidHex, OutlineColor.ValidHex, FillColor.ValidHex);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(saved));
            File.Move(_path + ".tmp", _path, true);
            SaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = "外观已应用，但无法保存到本地。";
        }
    }

    private sealed record Preferences(double FontSize, double OpacityPercent, double OutlineWidth,
        string TextColor, string OutlineColor, string FillColor);
}

// 颜色输入允许任意 RGB 色值；输入途中保留最后有效颜色，避免歌词闪烁或异常。
public sealed class DesktopLyricColor(string label, string initialHex) : ObservableObject
{
    private string _hex = initialHex;
    public string Label { get; } = label;
    public Color Value { get; private set; } = Color.Parse(initialHex);
    // 调色盘和色值输入共享同一个有效颜色，实时预览与持久化沿用原有路径。
    public Color SelectedColor
    {
        get => Value;
        set => Hex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    }
    public IBrush Brush => new SolidColorBrush(Value);
    public string ValidHex => $"#{Value.R:X2}{Value.G:X2}{Value.B:X2}";
    public bool HasError => !IsValid(_hex);
    public string Hex
    {
        get => _hex;
        set
        {
            if (_hex == value) return;
            _hex = value ?? string.Empty;
            if (IsValid(_hex)) Value = Color.Parse(_hex);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedColor));
            OnPropertyChanged(nameof(Brush));
            OnPropertyChanged(nameof(HasError));
        }
    }

    private static bool IsValid(string? value)
        => value is { Length: 7 } && value[0] == '#' && Color.TryParse(value, out _);
}
