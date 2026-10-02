// 模块：沉浸歌词排版偏好；独立保存，实时预览，不改变播放时钟或全局字体。
using System.Text.Json;
using Avalonia.Threading;

namespace SkyMusic.App.ViewModels;

public sealed class LyricsAppearance : ObservableObject, IDisposable
{
    private readonly string _path;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private double _fontSize = 28, _lineSpacing = 24, _letterSpacing, _translationSpacing = 6;
    private double _translationSizePercent = 80;
    private double _fullScreenPaddingPercent = 24;
    private int _currentWeightIndex = 3, _otherWeightIndex;
    private bool _dirty;
    private string? _saveError;
    public static IReadOnlyList<string> WeightLabels { get; } = ["常规", "中等", "半粗", "粗体", "特粗"];
    public IReadOnlyList<string> Weights => WeightLabels;
    public static Avalonia.Media.FontWeight Weight(int index) => (Avalonia.Media.FontWeight)new[] { 400, 500, 600, 700, 800 }[Math.Clamp(index, 0, 4)];

    public LyricsAppearance(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyMusicPlay", "lyrics-appearance.json");
        try
        {
            if (File.Exists(_path) && JsonSerializer.Deserialize<Preferences>(File.ReadAllText(_path)) is { } saved)
            {
                FontSize = saved.FontSize;
                LineSpacing = saved.LineSpacing;
                LetterSpacing = saved.LetterSpacing;
                TranslationSpacing = saved.TranslationSpacing;
                CurrentWeightIndex = saved.CurrentWeightIndex;
                OtherWeightIndex = saved.OtherWeightIndex;
                TranslationSizePercent = saved.TranslationSizePercent;
                FullScreenPaddingPercent = saved.FullScreenPaddingPercent;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { _saveError = "外观设置读取失败，已使用默认值。"; }
        _saveTimer.Tick += (_, _) => Save();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SaveError)) return;
            _dirty = true;
            _saveTimer.Stop();
            _saveTimer.Start();
        };
        ResetCommand = new RelayCommand(_ =>
        {
            FontSize = 28; LineSpacing = 24; LetterSpacing = 0;
            TranslationSpacing = 6; CurrentWeightIndex = 3; OtherWeightIndex = 0;
            TranslationSizePercent = 80;
            FullScreenPaddingPercent = 24;
        });
    }

    public double FontSize { get => _fontSize; set => SetProperty(ref _fontSize, Bound(value, 18, 56, 28)); }
    // 上下对称留白按可用歌词区域百分比计算，跨分辨率保持相同观看比例。
    public double FullScreenPaddingPercent { get => _fullScreenPaddingPercent; set => SetProperty(ref _fullScreenPaddingPercent, Bound(value, 5, 40, 24)); }
    public double LineSpacing { get => _lineSpacing; set => SetProperty(ref _lineSpacing, Bound(value, 0, 64, 24)); }
    public double LetterSpacing { get => _letterSpacing; set => SetProperty(ref _letterSpacing, Bound(value, 0, 8, 0)); }
    public double TranslationSpacing { get => _translationSpacing; set => SetProperty(ref _translationSpacing, Bound(value, 0, 24, 6)); }
    // 译文相对原文字号独立设置，旧配置缺少该字段时使用更易阅读的 80%。
    public double TranslationSizePercent { get => _translationSizePercent; set => SetProperty(ref _translationSizePercent, Bound(value, 60, 100, 80)); }
    public int CurrentWeightIndex { get => _currentWeightIndex; set => SetProperty(ref _currentWeightIndex, Math.Clamp(value, 0, 4)); }
    public int OtherWeightIndex { get => _otherWeightIndex; set => SetProperty(ref _otherWeightIndex, Math.Clamp(value, 0, 4)); }
    public string? SaveError { get => _saveError; private set => SetProperty(ref _saveError, value); }
    public RelayCommand ResetCommand { get; }
    private static double Bound(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    // 滑动过程中合并写入；关闭程序时补写最后一次修改，原子替换避免半份 JSON。
    public void Save()
    {
        _saveTimer.Stop();
        if (!_dirty) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(new Preferences(FontSize, LineSpacing,
                LetterSpacing, TranslationSpacing, CurrentWeightIndex, OtherWeightIndex, TranslationSizePercent, FullScreenPaddingPercent)));
            File.Move(_path + ".tmp", _path, true);
            _dirty = false;
            SaveError = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { SaveError = "外观已应用，但无法保存到本地。"; }
    }
    public void Dispose() { _saveTimer.Stop(); Save(); }
    private sealed record Preferences(double FontSize = 28, double LineSpacing = 24, double LetterSpacing = 0,
        double TranslationSpacing = 6, int CurrentWeightIndex = 3, int OtherWeightIndex = 0, double TranslationSizePercent = 80,
        double FullScreenPaddingPercent = 24);
}
