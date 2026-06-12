using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroExercise.ApiClient;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Enums;
using MicroExercise.Maui.Pages;

namespace MicroExercise.Maui.ViewModels;

/// <summary>Dropdown option for the add-exercise picker: a catalog type, or the "new custom" sentinel.</summary>
public sealed class PoolTypeOption
{
    private readonly ExerciseTypeDto? _type;

    private PoolTypeOption(ExerciseTypeDto? type) => _type = type;

    public static readonly PoolTypeOption Custom = new(null);
    public static PoolTypeOption For(ExerciseTypeDto type) => new(type);

    public bool IsCustomSentinel => _type is null;
    public int TypeId => _type?.Id ?? -1;

    public string Label => _type is null
        ? "➕ New custom exercise…"
        : $"{_type.Name} ({(_type.DefaultTrackingType == TrackingType.Seconds ? "sec" : "reps")})" +
          (_type.IsCustom ? " · custom" : "");
}

/// <summary>
/// The Pool screen — manage the quick-log grid. Mirrors the web <c>Pool.razor</c>: add from the
/// catalog (or create a new custom exercise), reorder up/down, edit (modal), and remove (soft
/// delete — history is preserved).
/// </summary>
public partial class PoolViewModel(PoolApi poolApi) : ObservableObject
{
    private Dictionary<int, string> _typeNames = new();

    public ObservableCollection<PoolTypeOption> Types { get; } = [];
    public ObservableCollection<PoolItemRow> Items { get; } = [];
    public string[] TrackingTypeOptions { get; } = ["Reps", "Time (sec)"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    private bool _isEmpty;

    public bool HasItems => !IsEmpty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomMode))]
    [NotifyPropertyChangedFor(nameof(ShowCustomFields))]
    [NotifyPropertyChangedFor(nameof(ShowOptionalName))]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    private PoolTypeOption? _selectedType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    private int _addTarget = 10;

    [ObservableProperty] private string? _addCustomName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    private string? _newName;

    [ObservableProperty] private int _newTrackingTypeIndex;

    public bool IsCustomMode => SelectedType?.IsCustomSentinel == true;
    public bool ShowCustomFields => IsCustomMode;
    public bool ShowOptionalName => SelectedType is not null && !IsCustomMode;
    public bool CanAdd => AddTarget > 0
        && SelectedType is not null
        && (!IsCustomMode || !string.IsNullOrWhiteSpace(NewName))
        && !IsLoading;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var previousTypeId = SelectedType?.TypeId;
            var types = await poolApi.GetExerciseTypesAsync();
            _typeNames = types.ToDictionary(t => t.Id, t => t.Name);

            Types.Clear();
            Types.Add(PoolTypeOption.Custom);
            foreach (var type in types)
                Types.Add(PoolTypeOption.For(type));
            SelectedType = Types.FirstOrDefault(o => o.TypeId == previousTypeId)
                ?? Types.Skip(1).FirstOrDefault()   // default to the first real catalog type
                ?? Types.FirstOrDefault();

            var pool = await poolApi.GetActivePoolAsync();
            Items.Clear();
            for (var i = 0; i < pool.Count; i++)
            {
                var item = pool[i];
                var typeName = _typeNames.GetValueOrDefault(item.ExerciseTypeId, item.DisplayName);
                Items.Add(new PoolItemRow(item, typeName, isFirst: i == 0, isLast: i == pool.Count - 1));
            }
            IsEmpty = Items.Count == 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (!CanAdd) return;
        IsLoading = true;
        try
        {
            if (IsCustomMode)
            {
                var tracking = NewTrackingTypeIndex == 1 ? TrackingType.Seconds : TrackingType.Reps;
                await poolApi.AddCustomExerciseAsync(new CreateCustomExerciseRequest(NewName!.Trim(), tracking, AddTarget));
                NewName = null;
                NewTrackingTypeIndex = 0;
            }
            else
            {
                await poolApi.AddPoolItemAsync(new CreatePoolItemRequest(SelectedType!.TypeId, AddTarget, AddCustomName));
                AddCustomName = null;
            }
            AddTarget = 10;
        }
        finally
        {
            IsLoading = false;
        }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task EditAsync(PoolItemRow? row)
    {
        if (row is null) return;
        var editor = new EditPoolItemViewModel(poolApi, row.Item, row.TypeName);
        await Shell.Current.Navigation.PushModalAsync(new EditPoolItemPage(editor));
    }

    [RelayCommand]
    private async Task MoveUpAsync(PoolItemRow? row)
    {
        if (row is null) return;
        await poolApi.MovePoolItemAsync(row.Id, up: true);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task MoveDownAsync(PoolItemRow? row)
    {
        if (row is null) return;
        await poolApi.MovePoolItemAsync(row.Id, up: false);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RemoveAsync(PoolItemRow? row)
    {
        if (row is null) return;
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Remove exercise?",
            $"{row.DisplayName} — its history is kept; it just leaves your dashboard.",
            "Remove", "Cancel");
        if (!confirmed) return;

        await poolApi.DeactivatePoolItemAsync(row.Id);
        await LoadAsync();
    }
}
