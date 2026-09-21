// 模块：SkyMusic.Backend.Tests 后端测试 JsonKeyMappingStoreTests
using SkyMusic.Core.Mapping;
using SkyMusic.Infrastructure.Input;

namespace SkyMusic.Backend.Tests;

public sealed class JsonKeyMappingStoreTests
{
    [Fact]
    public async Task SavesUpdatesAndDeletesMappings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "mappings.json");
        try
        {
            var store = new JsonKeyMappingStore(path);
            await store.SaveAsync(new KeyMappingDefinition("custom", "First", [new KeyMappingEntry(60, 30)]));
            await store.SaveAsync(new KeyMappingDefinition("custom", "Updated", [new KeyMappingEntry(62, 31)]));

            var mapping = Assert.Single(await store.LoadAsync());
            Assert.Equal("Updated", mapping.Name);
            Assert.Equal(62, Assert.Single(mapping.Entries).MidiNote);

            await store.DeleteAsync("custom");
            Assert.Empty(await store.LoadAsync());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public async Task RejectsDuplicateMidiNotes()
    {
        var path = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"), "mappings.json");
        var store = new JsonKeyMappingStore(path);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await store.SaveAsync(new KeyMappingDefinition(
                "duplicate",
                "Duplicate",
                [new KeyMappingEntry(60, 30), new KeyMappingEntry(60, 31)])));
    }
}
