// 模块：SkyMusic.Infrastructure 输入设备 KeyboardMappingProfile
using SkyMusic.Core.Mapping;

namespace SkyMusic.Infrastructure.Input;

public sealed class KeyboardMappingProfile
{
    public KeyboardMappingProfile(string name, IReadOnlyDictionary<int, ScanCodeBinding> bindings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bindings);

        foreach (var (midiNote, binding) in bindings)
        {
            if (midiNote is < 21 or > 108)
            {
                throw new ArgumentOutOfRangeException(nameof(bindings), midiNote, "MIDI note must be within the 88-key piano range");
            }

            if (binding.ScanCode == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bindings), binding.ScanCode, "Scan code cannot be zero");
            }
        }

        Name = name;
        Bindings = new Dictionary<int, ScanCodeBinding>(bindings);
    }

    public string Name { get; }

    public IReadOnlyDictionary<int, ScanCodeBinding> Bindings { get; }

    public bool TryGetBinding(int midiNote, out ScanCodeBinding binding) =>
        Bindings.TryGetValue(midiNote, out binding);

    public static KeyboardMappingProfile FromDefinition(KeyMappingDefinition definition) =>
        new(
            definition.Name,
            definition.Entries.ToDictionary(
                entry => entry.MidiNote,
                entry => new ScanCodeBinding(entry.ScanCode, entry.IsExtended)));
}
