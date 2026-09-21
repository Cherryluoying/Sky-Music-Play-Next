// 模块：SkyMusic.Infrastructure 输入设备 JsonKeyMappingStore
using System.Text.Json;
using SkyMusic.Core.Mapping;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Input;

public sealed class JsonKeyMappingStore(string filePath) : IKeyMappingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<IReadOnlyList<KeyMappingDefinition>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SaveAsync(KeyMappingDefinition mapping, CancellationToken cancellationToken = default)
    {
        Validate(mapping);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var mappings = (await ReadCoreAsync(cancellationToken)).ToList();
            mappings.RemoveAll(item => string.Equals(item.Id, mapping.Id, StringComparison.OrdinalIgnoreCase));
            mappings.Add(mapping);
            await WriteCoreAsync(mappings, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var mappings = (await ReadCoreAsync(cancellationToken)).ToList();
            if (mappings.RemoveAll(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                await WriteCoreAsync(mappings, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<IReadOnlyList<KeyMappingDefinition>> ReadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(filePath);
        var mappings = await JsonSerializer.DeserializeAsync<List<KeyMappingDefinition>>(stream, JsonOptions, cancellationToken) ?? [];
        foreach (var mapping in mappings)
        {
            Validate(mapping);
        }
        return mappings;
    }

    private static void Validate(KeyMappingDefinition mapping)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapping.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapping.Name);
        if (mapping.Entries.Count == 0)
        {
            throw new ArgumentException("Mapping must contain at least one key", nameof(mapping));
        }

        foreach (var entry in mapping.Entries)
        {
            if (entry.MidiNote is < 21 or > 108 || entry.ScanCode == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(mapping), "Mapping contains an invalid MIDI note or scan code");
            }
        }

        if (mapping.Entries.Select(entry => entry.MidiNote).Distinct().Count() != mapping.Entries.Count)
        {
            throw new ArgumentException("Mapping contains duplicate MIDI notes", nameof(mapping));
        }
    }

    private async ValueTask WriteCoreAsync(
        IReadOnlyList<KeyMappingDefinition> mappings,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, mappings, JsonOptions, cancellationToken);
        }

        // 同卷替换避免写入中断留下半个 JSON
        File.Move(temporaryPath, filePath, true);
    }
}
