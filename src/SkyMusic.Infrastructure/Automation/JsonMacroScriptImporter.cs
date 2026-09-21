// 模块：SkyMusic.Infrastructure 宏脚本领域 JsonMacroScriptImporter
using System.Globalization;
using System.Text.Json;
using SkyMusic.Core.Automation;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Automation;

public sealed class JsonMacroScriptImporter : IMacroScriptImporter
{
    public const int MaximumEventCount = 100_000;
    public const int MaximumFileBytes = 16 * 1024 * 1024;
    public static readonly TimeSpan MaximumEventDelay = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(24);

    // 只导入允许的键盘事件并拒绝危险宏命令
    public async ValueTask<MacroImportResult> ImportAsync(
        Stream source,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.CanSeek && source.Length > MaximumFileBytes)
        {
            return MacroImportResult.Failed("宏脚本不能超过 16 MB");
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(source, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return MacroImportResult.Failed("宏脚本根节点必须是数组");
            }

            if (document.RootElement.GetArrayLength() is 0 or > MaximumEventCount)
            {
                return MacroImportResult.Failed($"宏脚本事件数量必须在 1 到 {MaximumEventCount} 之间");
            }

            var events = new List<MacroEvent>(document.RootElement.GetArrayLength());
            var errors = new List<string>();
            long timeMicroseconds = 0;
            var index = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryReadEvent(item, out var key, out var action, out var delayMilliseconds, out var error))
                {
                    errors.Add($"第 {index + 1} 项：{error}");
                    if (errors.Count == 20)
                    {
                        errors.Add("错误过多，已停止检查");
                        break;
                    }

                    index++;
                    continue;
                }

                timeMicroseconds = checked(timeMicroseconds + (delayMilliseconds * 1_000));
                if (timeMicroseconds > MaximumDuration.TotalMicroseconds)
                {
                    errors.Add($"第 {index + 1} 项：宏脚本总时长不能超过 24 小时");
                    break;
                }

                events.Add(new MacroEvent(key, action, timeMicroseconds));
                index++;
            }

            return errors.Count == 0
                ? new MacroImportResult(new MacroScript(Path.GetFileNameWithoutExtension(sourceName), events), [])
                : new MacroImportResult(null, errors);
        }
        catch (JsonException exception)
        {
            return MacroImportResult.Failed($"宏脚本 JSON 无效：{exception.Message}");
        }
        catch (OverflowException)
        {
            return MacroImportResult.Failed("宏脚本延迟数值过大");
        }
    }

    // 校验单条宏事件并归一化键名和延迟
    private static bool TryReadEvent(
        JsonElement item,
        out string key,
        out MacroKeyAction action,
        out long delayMilliseconds,
        out string error)
    {
        key = string.Empty;
        action = default;
        delayMilliseconds = 0;
        error = string.Empty;
        if (item.ValueKind != JsonValueKind.Object)
        {
            error = "事件必须是对象";
            return false;
        }

        if (!item.TryGetProperty("key", out var keyValue) || keyValue.ValueKind != JsonValueKind.String ||
            !MacroKeyCatalog.TryNormalize(keyValue.GetString(), out key))
        {
            error = "key 不是允许的键名";
            return false;
        }

        if (!item.TryGetProperty("type", out var typeValue) || typeValue.ValueKind != JsonValueKind.String ||
            !Enum.TryParse(typeValue.GetString(), true, out action))
        {
            error = "type 必须是 Down 或 Up";
            return false;
        }

        if (!item.TryGetProperty("delay", out var delayValue) || !TryReadDelay(delayValue, out delayMilliseconds) ||
            delayMilliseconds < 0 || delayMilliseconds > MaximumEventDelay.TotalMilliseconds)
        {
            error = "delay 必须是 0 到 3600000 的毫秒整数";
            return false;
        }

        return true;
    }

    private static bool TryReadDelay(JsonElement value, out long delay)
    {
        delay = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out delay) ||
               value.ValueKind == JsonValueKind.String &&
               long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out delay);
    }
}
