using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Enums;

namespace MicroExercise.Maui.ViewModels;

/// <summary>
/// One quick-log card: a pool item plus its pending amount and today's running totals. The native
/// twin of the web <c>QuickLogCard</c> — a +/- stepper (step 5 for seconds, 1 for reps) and a big
/// log button. Logging itself lives on <see cref="LogViewModel"/>; this holds only local card state.
/// </summary>
public partial class QuickLogItem : ObservableObject
{
    public PoolItemDto Item { get; }

    public QuickLogItem(PoolItemDto item)
    {
        Item = item;
        Pending = item.TargetQuantity;
    }

    public int Id => Item.Id;
    public string DisplayName => Item.DisplayName;
    public string Unit => Item.TrackingType == TrackingType.Seconds ? "sec" : "reps";
    public int Step => Item.TrackingType == TrackingType.Seconds ? 5 : 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingLabel))]
    [NotifyPropertyChangedFor(nameof(CanDecrement))]
    private int _pending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TodaySummary))]
    private int _todayBursts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TodaySummary))]
    private long _todayVolume;

    public string PendingLabel => $"{Pending} {Unit}";
    public string TodaySummary => $"{TodayBursts} today · {TodayVolume} {Unit} total";
    public bool CanDecrement => Pending > Step;

    [RelayCommand]
    private void Increment() => Pending += Step;

    [RelayCommand]
    private void Decrement() => Pending = Math.Max(Step, Pending - Step);

    /// <summary>Set the day's starting totals from the report summary (no burst happened).</summary>
    public void Seed(int bursts, long volume)
    {
        TodayBursts = bursts;
        TodayVolume = volume;
    }

    /// <summary>Optimistically fold a just-logged burst into today's totals.</summary>
    public void RecordBurst(int quantity)
    {
        TodayBursts++;
        TodayVolume += quantity;
    }
}
