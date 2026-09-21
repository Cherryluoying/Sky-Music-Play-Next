// 模块：SkyMusic.Infrastructure 应用设置 JsonAppSettingsStore
using System.Text.Json;
using System.Text.Json.Serialization;
using SkyMusic.Core.Services;
using SkyMusic.Core.Settings;

namespace SkyMusic.Infrastructure.Settings;

public sealed class JsonAppSettingsStore(string filePath) : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                return new AppSettings();
            }

            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                ?? new AppSettings();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = filePath + ".tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            }

            // 同卷替换避免设置文件只写入一部分
            File.Move(temporaryPath, filePath, true);
        }
        finally
        {
            _gate.Release();
        }
    }
}
