// 模块：SkyMusic.App 界面模型 AppPage
namespace SkyMusic.App.Models;

public enum AppPage
{
    Discover,
    Library,
    Favorites,
    Recent,
    Tasks,
    MidiStudio,
    ScoreEditor,
    KeyMapping,
    Transcription,
    // 保留原有设置页编号，已移除宏脚本页面。
    Settings = 10,
    Imported = 11
}
