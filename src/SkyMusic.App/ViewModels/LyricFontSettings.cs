// 模块：用户歌词字体库；字体只复制到应用目录，不安装到 Windows，歌词与桌面歌词独立选择。
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace SkyMusic.App.ViewModels;

public sealed record LyricFontOption(string Id, string Name, FontFamily Family);

public sealed class LyricFontSettings : ObservableObject, IDisposable
{
    public static FontFamily DefaultFamily { get; } = new("avares://SkyMusic.App/Assets/fonts/OPPO-Sans.ttf#OPPO Sans 4.0");
    private readonly string _directory;
    private readonly List<Uri> _collections = [];
    private LyricFontOption _lyricsFont;
    private LyricFontOption _desktopFont;
    private LyricFontOption _pageFont;
    private LyricFontOption _desktopSessionFont;
    private string? _status;
    private bool _importing;
    private bool _disposed;

    public LyricFontSettings(string? directory = null)
    {
        _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyMusicPlay", "fonts");
        _pageFont = new("follow-default", "跟随设置默认字体", DefaultFamily);
        _desktopSessionFont = _pageFont;
        PageFontOptions.Add(_pageFont);
        AddFont(new("default", "内置 · OPPO Sans", DefaultFamily));
        _lyricsFont = _desktopFont = Fonts[0];
        try
        {
            if (!Directory.Exists(_directory)) return;
            foreach (var path in Directory.EnumerateFiles(_directory).Where(IsFontFile))
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    AddFont(Register(Path.GetFileName(path), stream));
                }
                catch (Exception) { Status = "部分字体无法读取，已跳过；可重新导入字体文件。"; }
            }
            var preferencesPath = Path.Combine(_directory, "selection.json");
            if (File.Exists(preferencesPath) && JsonSerializer.Deserialize<Preferences>(File.ReadAllText(preferencesPath)) is { } saved)
            {
                _lyricsFont = Fonts.FirstOrDefault(font => font.Id == saved.Lyrics) ?? Fonts[0];
                _desktopFont = Fonts.FirstOrDefault(font => font.Id == saved.Desktop) ?? Fonts[0];
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { Status = "字体设置读取失败，已使用可用字体。"; }
    }

    public ObservableCollection<LyricFontOption> Fonts { get; } = [];
    public ObservableCollection<LyricFontOption> PageFontOptions { get; } = [];

    // 页内选择仅在本次运行生效，普通与全屏共用，不覆盖设置页保存的默认值。
    public LyricFontOption PageFont
    {
        get => _pageFont;
        set
        {
            if (value is not null && SetProperty(ref _pageFont, value))
                OnPropertyChanged(nameof(EffectiveLyricsFont));
        }
    }
    public LyricFontOption EffectiveLyricsFont => PageFont.Id == "follow-default" ? LyricsFont : PageFont;

    // 桌面歌词独立选择本次运行的字体；与歌词页共享字体库，但不共享选择或覆盖默认值。
    public LyricFontOption DesktopSessionFont
    {
        get => _desktopSessionFont;
        set
        {
            if (value is not null && SetProperty(ref _desktopSessionFont, value))
                OnPropertyChanged(nameof(EffectiveDesktopFont));
        }
    }
    public LyricFontOption EffectiveDesktopFont => DesktopSessionFont.Id == "follow-default" ? DesktopFont : DesktopSessionFont;

    // 设置页修改默认字体时，仅跟随默认的歌词页同步更新。
    public LyricFontOption LyricsFont
    {
        get => _lyricsFont;
        set
        {
            if (value is null || !SetProperty(ref _lyricsFont, value)) return;
            if (PageFont.Id == "follow-default") OnPropertyChanged(nameof(EffectiveLyricsFont));
            Save();
        }
    }
    public LyricFontOption DesktopFont
    {
        get => _desktopFont;
        set
        {
            if (value is null || !SetProperty(ref _desktopFont, value)) return;
            if (DesktopSessionFont.Id == "follow-default") OnPropertyChanged(nameof(EffectiveDesktopFont));
            Save();
        }
    }
    public string? Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool IsImporting { get => _importing; private set => SetProperty(ref _importing, value); }

    // 校验格式后按内容哈希保存副本；同一字体重复导入不产生重复选项，源文件移动不影响播放。
    public async Task ImportAsync(string path)
    {
        if (IsImporting || _disposed) return;
        IsImporting = true;
        Uri? registeredKey = null;
        try
        {
            if (!IsFontFile(path)) throw new ArgumentException("请选择 TTF 或 OTF 字体文件。");
            await using var input = File.OpenRead(path);
            if (input.Length is <= 0 or > 64 * 1024 * 1024) throw new ArgumentException("字体文件为空或超过 64 MB。");
            var bytes = new byte[checked((int)input.Length)];
            await input.ReadExactlyAsync(bytes);
            if (_disposed) return;
            var id = Convert.ToHexString(SHA256.HashData(bytes)) + Path.GetExtension(path).ToLowerInvariant();
            if (Fonts.Any(font => font.Id == id)) { Status = "该字体已导入，可在下方选择。"; return; }
            using var stream = new MemoryStream(bytes, false);
            var option = Register(id, stream);
            registeredKey = _collections[^1];
            Directory.CreateDirectory(_directory);
            var destination = Path.Combine(_directory, id);
            await File.WriteAllBytesAsync(destination + ".tmp", bytes);
            File.Move(destination + ".tmp", destination, true);
            if (_disposed) return;
            AddFont(option);
            registeredKey = null;
            Status = $"已导入 {option.Name}，可设为默认字体，也可在歌词页单独选择。";
        }
        catch (Exception ex) { Status = $"字体导入失败：{ex.Message}"; }
        finally
        {
            // 磁盘写入失败时回收已验证但未发布的字体，反复重试不会累积原生字形缓存。
            if (registeredKey is not null && _collections.Remove(registeredKey)) FontManager.Current.RemoveFontCollection(registeredKey);
            IsImporting = false;
        }
    }

    public void ReportImportError(string message) => Status = $"字体导入失败：{message}";

    // 两个选择入口共享导入结果；新增字体不会重置当前页内选择。
    private void AddFont(LyricFontOption font)
    {
        Fonts.Add(font);
        PageFontOptions.Add(font);
    }
    private static bool IsFontFile(string path) => Path.GetExtension(path).ToLowerInvariant() is ".ttf" or ".otf";

    private LyricFontOption Register(string id, Stream stream)
    {
        var collection = new ImportedFontCollection(new Uri($"fonts:sky-lyrics-{Guid.NewGuid():N}"), stream);
        FontManager.Current.AddFontCollection(collection);
        _collections.Add(collection.Key);
        return new(id, collection.Family.Name, collection.Family);
    }

    private void Save()
    {
        if (_disposed) return;
        try
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, "selection.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new Preferences(LyricsFont.Id, DesktopFont.Id)));
            File.Move(path + ".tmp", path, true);
            Status = "默认字体已保存；歌词页选择跟随默认时同步生效。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Status = "默认字体已更新，但无法保存到本地。"; }
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var key in _collections) FontManager.Current.RemoveFontCollection(key);
        _collections.Clear();
    }

    private sealed record Preferences(string Lyrics = "default", string Desktop = "default");

    // 使用 Avalonia 的字形缓存和回退机制，描边桌面歌词与普通 TextBlock 使用同一个字体源。
    private sealed class ImportedFontCollection : FontCollectionBase
    {
        public override Uri Key { get; }
        public FontFamily Family { get; }
        public ImportedFontCollection(Uri key, Stream stream)
        {
            Key = key;
            if (!TryAddGlyphTypeface(stream, out var glyph)) throw new ArgumentException("文件不是可用的字体，或字体已损坏。");
            Family = new FontFamily($"{key}#{glyph.FamilyName}");
            AddFontFamily(Family);
        }
    }
}
