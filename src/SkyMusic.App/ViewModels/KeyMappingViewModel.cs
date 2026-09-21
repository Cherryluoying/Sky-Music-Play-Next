// 模块：SkyMusic.App 界面状态 KeyMappingViewModel
using System.Collections.ObjectModel;
using SkyMusic.Core.Mapping;
using SkyMusic.Core.Services;

namespace SkyMusic.App.ViewModels;

public sealed class KeyMappingViewModel : ObservableObject
{
    private readonly IKeyMappingStore _store;
    private KeyMappingProfileItemViewModel? _selectedProfile;
    private string _mappingName = "自定义映射";
    private string _processNames = "";
    private string _statusText = "映射使用 Windows 扫描码，保存后重启应用生效";

    public KeyMappingViewModel(IKeyMappingStore store, IReadOnlyList<KeyMappingDefinition> initialMappings)
    {
        _store = store;
        NewCommand = new RelayCommand(_ => NewMapping());
        AddEntryCommand = new RelayCommand(_ => AddEntry());
        RemoveEntryCommand = new RelayCommand(item => RemoveEntry(item as KeyMappingEntryViewModel));
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), _ => Entries.Count > 0, SetError);
        DeleteCommand = new AsyncRelayCommand(_ => DeleteAsync(), _ => SelectedProfile is not null, SetError);
        ReplaceProfiles(initialMappings);
    }

    public ObservableCollection<KeyMappingProfileItemViewModel> Profiles { get; } = [];

    public ObservableCollection<KeyMappingEntryViewModel> Entries { get; } = [];

    public RelayCommand NewCommand { get; }

    public RelayCommand AddEntryCommand { get; }

    public RelayCommand RemoveEntryCommand { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand DeleteCommand { get; }

    public KeyMappingProfileItemViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value))
            {
                return;
            }

            if (value is not null)
            {
                LoadDefinition(value.Definition);
            }
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    public string MappingName
    {
        get => _mappingName;
        set => SetProperty(ref _mappingName, value);
    }

    public string ProcessNames
    {
        get => _processNames;
        set => SetProperty(ref _processNames, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private void NewMapping()
    {
        SelectedProfile = null;
        MappingName = "自定义映射";
        ProcessNames = "";
        Entries.Clear();
        AddEntry();
        StatusText = "已创建空白映射";
    }

    private void AddEntry()
    {
        var nextNote = Entries.Count == 0 ? 60 : Math.Min(108, Entries.Max(entry => entry.MidiNote) + 1);
        Entries.Add(new KeyMappingEntryViewModel(nextNote, 30, false));
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void RemoveEntry(KeyMappingEntryViewModel? entry)
    {
        if (entry is not null)
        {
            Entries.Remove(entry);
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    // 校验并持久化当前自定义键位方案
    private async Task SaveAsync()
    {
        var id = SelectedProfile?.Definition.Id ?? Guid.NewGuid().ToString("N");
        var definition = new KeyMappingDefinition(
            id,
            string.IsNullOrWhiteSpace(MappingName) ? "自定义映射" : MappingName.Trim(),
            Entries.GroupBy(entry => entry.MidiNote)
                .Select(group => group.Last())
                .OrderBy(entry => entry.MidiNote)
                .Select(entry => new KeyMappingEntry(
                    entry.MidiNote,
                    checked((ushort)entry.ScanCode),
                    entry.IsExtended))
                .ToArray(),
            ProcessNames.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        await _store.SaveAsync(definition);
        await ReloadAsync(id);
        StatusText = "映射已保存，重启应用后会出现在演奏目标中";
    }

    private async Task DeleteAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }
        await _store.DeleteAsync(SelectedProfile.Definition.Id);
        await ReloadAsync(null);
        NewMapping();
        StatusText = "映射已删除";
    }

    // 重新加载全部方案并恢复选中项
    private async Task ReloadAsync(string? selectedId)
    {
        var mappings = await _store.LoadAsync();
        ReplaceProfiles(mappings);
        SelectedProfile = Profiles.FirstOrDefault(item => item.Definition.Id == selectedId);
    }

    private void ReplaceProfiles(IEnumerable<KeyMappingDefinition> mappings)
    {
        Profiles.Clear();
        foreach (var mapping in mappings.OrderBy(mapping => mapping.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Profiles.Add(new KeyMappingProfileItemViewModel(mapping));
        }
    }

    private void LoadDefinition(KeyMappingDefinition definition)
    {
        MappingName = definition.Name;
        ProcessNames = string.Join(", ", definition.WindowProcessNames ?? []);
        Entries.Clear();
        foreach (var entry in definition.Entries)
        {
            Entries.Add(new KeyMappingEntryViewModel(entry.MidiNote, entry.ScanCode, entry.IsExtended));
        }
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void SetError(Exception exception) => StatusText = exception.Message;
}
