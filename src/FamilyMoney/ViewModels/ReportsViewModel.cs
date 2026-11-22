using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace FamilyMoney.ViewModels;

/// <summary>Режимы раздела «Отчёты».</summary>
public enum ReportMode
{
    Timeline,
    TopExpenses,
    TopIncomes,
}

/// <summary>
/// Раздел «Отчёты»: переключатель режимов и три независимых отчёта —
/// «Таймлайн», «Топ расходов» и «Топ доходов». У каждого свои фильтры.
/// </summary>
public partial class ReportsViewModel : ViewModelBase
{
    public ReportsViewModel(
        TimelineViewModel timeline,
        TopExpensesViewModel topExpenses,
        TopIncomesViewModel topIncomes)
    {
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        TopExpenses = topExpenses ?? throw new ArgumentNullException(nameof(topExpenses));
        TopIncomes = topIncomes ?? throw new ArgumentNullException(nameof(topIncomes));
    }

    public TimelineViewModel Timeline { get; }

    public TopExpensesViewModel TopExpenses { get; }

    public TopIncomesViewModel TopIncomes { get; }

    [ObservableProperty]
    public partial ReportMode Mode { get; set; } = ReportMode.Timeline;

    public bool IsTimeline => Mode == ReportMode.Timeline;

    public bool IsTopExpenses => Mode == ReportMode.TopExpenses;

    public bool IsTopIncomes => Mode == ReportMode.TopIncomes;

    [RelayCommand]
    private void ShowTimeline() => Mode = ReportMode.Timeline;

    [RelayCommand]
    private void ShowTopExpenses() => Mode = ReportMode.TopExpenses;

    [RelayCommand]
    private void ShowTopIncomes() => Mode = ReportMode.TopIncomes;

    /// <summary>Вход в раздел: шкала открывается заново от периода главного экрана.</summary>
    public void Enter()
    {
        Timeline.Enter();
        TopExpenses.Enter();
        TopIncomes.Enter();
    }

    /// <summary>Полная перезагрузка — например, после смены базы данных.</summary>
    public void Reload()
    {
        Timeline.Reload();
        TopExpenses.Reload();
        TopIncomes.Reload();
    }

    partial void OnModeChanged(ReportMode value)
    {
        OnPropertyChanged(nameof(IsTimeline));
        OnPropertyChanged(nameof(IsTopExpenses));
        OnPropertyChanged(nameof(IsTopIncomes));
    }
}
