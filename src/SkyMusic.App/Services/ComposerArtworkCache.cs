// 模块：SkyMusic.App 桌面服务 ComposerArtworkCache
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkyMusic.Core.GameScores;

namespace SkyMusic.App.Services;

internal static class ComposerArtworkCache
{
    private static readonly Lazy<Bitmap?> Sky = new(() => Load("Sky"));
    private static readonly Lazy<Bitmap?> Genshin = new(() => Load("Genshin"));
    private static readonly Lazy<Bitmap?> SkyBackground = new(() => LoadTheme("Rainy_Theme.png"));
    private static readonly Lazy<Bitmap?> GenshinBackground = new(() => LoadTheme("Liyue_Theme.jpg"));
    private static readonly Lazy<Bitmap?> CustomBackground = new(() => LoadTheme("Legacy_Bg_Theme.png"));

    public static Bitmap? Get(GameScoreProfile profile) =>
        profile == GameScoreProfile.Genshin ? Genshin.Value : Sky.Value;

    public static Bitmap? GetBackground(GameScoreProfile profile) => profile switch
    {
        GameScoreProfile.Genshin => GenshinBackground.Value,
        GameScoreProfile.Custom => CustomBackground.Value,
        _ => SkyBackground.Value
    };

    private static Bitmap? Load(string profile)
    {
        try
        {
            var uri = new Uri(
                $"avares://SkyMusic.App/Assets/GenshinMusic/Images/App/{profile}/composerIcon.png");
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            // 素材缺失时由文字图标降级
            return null;
        }
    }

    private static Bitmap? LoadTheme(string fileName)
    {
        try
        {
            var uri = new Uri(
                $"avares://SkyMusic.App/Assets/GenshinMusic/Images/Themes/{fileName}");
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
