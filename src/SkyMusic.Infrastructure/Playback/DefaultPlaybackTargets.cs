// 模块：SkyMusic.Infrastructure 播放领域 DefaultPlaybackTargets
using SkyMusic.Core.Playback;
using SkyMusic.Core.Mapping;
using SkyMusic.Infrastructure.Input;

namespace SkyMusic.Infrastructure.Playback;

public static class DefaultPlaybackTargets
{
    public static IReadOnlyList<PlaybackTargetRegistration> Create(
        IEnumerable<KeyMappingDefinition>? customMappings = null)
    {
        const PlaybackSinkCapabilities keyboardCapabilities =
            PlaybackSinkCapabilities.ForegroundInput |
            PlaybackSinkCapabilities.ScanCodes |
            PlaybackSinkCapabilities.SimultaneousKeys;

        var targets = new List<PlaybackTargetRegistration>
        {
            new(
                new PlaybackTarget(
                    "sky-15",
                    "光遇 15 键",
                    "兼容原版 YUIOP / HJKL; / NM,./ 键位",
                    15,
                    keyboardCapabilities,
                    ["Sky", "Sky-Windows", "SkyClient"]),
                () => new WindowsScanCodeSink(LegacySky15KeyProfile.Create())),
            new(
                new PlaybackTarget(
                    "genshin-21",
                    "原神 21 键",
                    "低中高三个七声音阶键盘布局",
                    21,
                    keyboardCapabilities,
                    ["YuanShen", "GenshinImpact", "Genshin"]),
                () => new WindowsScanCodeSink(Genshin21KeyProfile.Create()))
        };

        foreach (var mapping in customMappings ?? [])
        {
            var capturedMapping = mapping;
            targets.Add(new PlaybackTargetRegistration(
                new PlaybackTarget(
                    $"custom:{mapping.Id}",
                    mapping.Name,
                    "自定义 88 键扫描码映射",
                    mapping.Entries.Select(entry => entry.MidiNote).Distinct().Count(),
                    keyboardCapabilities,
                    mapping.WindowProcessNames ?? []),
                () => new WindowsScanCodeSink(KeyboardMappingProfile.FromDefinition(capturedMapping))));
        }

        try
        {
            foreach (var device in new MidiOutputDeviceCatalog().GetAvailableDevices())
            {
                var capturedIndex = device.Index;
                targets.Add(new PlaybackTargetRegistration(
                    new PlaybackTarget(
                        $"midi:{device.Index}",
                        $"MIDI · {device.Name}",
                        "标准 MIDI 输出，保留完整 88 键音域",
                        88,
                        PlaybackSinkCapabilities.MidiOutput | PlaybackSinkCapabilities.SimultaneousKeys),
                    () => new MidiOutputSink(capturedIndex)));
            }
        }
        catch (Exception) when (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            // MIDI 设备发现失败时保留键盘输出
        }

        return targets;
    }
}
