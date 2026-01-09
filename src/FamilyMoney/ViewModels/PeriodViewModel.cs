using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.State;
using System;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public enum PeriodType
{
    Custom,
    Day,
    Month,
    Quarter,
    Year,
    All,
}

public partial class PeriodViewModel: ViewModelBase
{
    private readonly IStateManager _stateManager;

    public PeriodViewModel(IStateManager stateManager)
    {
        _stateManager = stateManager;

        From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        To = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1);
        PeriodType = PeriodType.Month;
    }

    [ObservableProperty]
    public partial PeriodType PeriodType { get; set; }

    [ObservableProperty]
    public partial DateTime From { get; set; }

    [ObservableProperty]
    public partial DateTime To { get; set; }

    public string Text
    {
        get
        {
            return PeriodType switch
            {
                PeriodType.Day => From.ToString("dd MMMM yyyy"),
                PeriodType.Month => From.ToString("MMMM yyyy"),
                PeriodType.Quarter => From.ToString($"Квартал {(From.Month + 2) / 3} {From.Year}"),
                PeriodType.Year => From.ToString("yyyy"),
                PeriodType.All => From.ToString("Всё время"),
                _ => From.ToString("dd.MM.yyyy") + " - " + To.ToString("dd.MM.yyyy"),
            };
        }
    }

    [RelayCommand]
    public async Task NextAsync()
    {
        Shift(1);
    }

    [RelayCommand]
    public async Task PrevAsync()
    {
        Shift(-1);
    }

    [RelayCommand]
    public async Task ToDayAsync()
    {
        From = DateTime.Today;
        To = DateTime.Today;
        PeriodType = PeriodType.Day;
        Update();
    }

    [RelayCommand]
    public async Task ToMonthAsync()
    {
        From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        To = From.AddMonths(1).AddDays(-1);
        PeriodType = PeriodType.Month;
        Update();
    }

    [RelayCommand]
    public async Task ToQuarterAsync()
    {
        var month = PeriodType != PeriodType.All ? To.Month : DateTime.Today.Month;
        var year = PeriodType != PeriodType.All ? To.Year : DateTime.Today.Year;

        switch ((month + 2) / 3)
        {
            case 1:
                From = new DateTime(year, 1, 1);
                To = From.AddMonths(3).AddDays(-1);
                break;
            case 2:
                From = new DateTime(year, 4, 1);
                To = From.AddMonths(3).AddDays(-1);
                break;
            case 3:
                From = new DateTime(year, 7, 1);
                To = From.AddMonths(3).AddDays(-1);
                break;
            case 4:
                From = new DateTime(year, 10, 1);
                To = From.AddMonths(3).AddDays(-1);
                break;
        }
        PeriodType = PeriodType.Quarter;
        Update();
    }

    [RelayCommand]
    public async Task ToYearAsync()
    {
        From = new DateTime(PeriodType != PeriodType.All ? To.Year : DateTime.Today.Year, 1, 1);
        To = new DateTime(PeriodType != PeriodType.All ? To.Year : DateTime.Today.Year, 12, 31);
        PeriodType = PeriodType.Year;
        Update();
    }

    [RelayCommand]
    public async Task ToCustomAsync()
    {
        var period = new CustomPeriodViewModel
        {
            From = PeriodType != PeriodType.All ? this.From : DateTime.Today,
            To = PeriodType != PeriodType.All ? this.To : DateTime.Today
        };

        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<CustomPeriodViewModel>(period));
        if (result != null)
        {
            PeriodType = PeriodType.Custom;
            From = result.From;
            To = result.To;
        }
        Update();

        PeriodType = PeriodType.Custom;
    }

    [RelayCommand]
    public async Task ToAllAsync()
    {
        From = DateTime.MinValue;
        To = DateTime.MaxValue;
        PeriodType = PeriodType.All;
        Update();
    }

    private void Shift(int direction)
    {
        switch (PeriodType)
        {
            case PeriodType.Day:
                From = From.AddDays(direction);
                To = To.AddDays(direction);
                break;
            case PeriodType.Month:
                From = From.AddMonths(direction);
                To = From.AddMonths(Math.Abs(direction)).AddDays(-1);
                break;
            case PeriodType.Quarter:
                From = From.AddMonths(direction * 3);
                To = From.AddMonths(3).AddDays(-1);
                break;
            case PeriodType.Year:
                From = From.AddYears(direction);
                To = From.AddYears(Math.Abs(direction));
                break;
            case PeriodType.Custom:
                var diff = (To - From).Days;
                From = From.AddDays(direction * diff);
                To = To.AddDays(Math.Abs(direction) * diff);
                break;
        }

        Update();
    }

    private void Update()
    {
        this.OnPropertyChanged(nameof(Text));
        var state = _stateManager.GetMainState();
        _stateManager.SetMainState(state with { PeriodFrom = From, PeriodTo = To });
    }
}
