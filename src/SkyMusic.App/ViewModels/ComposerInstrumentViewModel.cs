// 模块：SkyMusic.App 界面状态 ComposerInstrumentViewModel
using Avalonia.Media.Imaging;
using SkyMusic.App.Services;
using SkyMusic.Core.GameScores;

namespace SkyMusic.App.ViewModels;

public sealed class ComposerInstrumentViewModel(
    GameInstrumentLayer instrument,
    int index,
    GameScoreProfile profile)
{
    public int Index => index;
    public string Name => instrument.Name;
    public string DisplayName => string.IsNullOrWhiteSpace(instrument.Alias)
        ? InstrumentDisplayNames.Get(instrument.Name)
        : instrument.Alias;
    public string Detail => $"音量 {instrument.Volume} · {instrument.Pitch}";
    public Bitmap? Artwork => ComposerArtworkCache.Get(profile);
    public string IconText => instrument.Icon switch
    {
        GameNoteIcon.Border => "□",
        GameNoteIcon.Line => "—",
        _ => "●"
    };
    public bool IsMuted => instrument.IsMuted;
    public bool IsVisible => instrument.IsVisible;
}
