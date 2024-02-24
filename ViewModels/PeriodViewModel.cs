using ReactiveUI;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public enum PeriodType
{
    Custom,
    Month,
    Quarter,
    Year,
    All,
}

public class PeriodViewModel: ViewModelBase
{
    private PeriodType _periodType;
    private DateTime _from; 
    private DateTime _to;

    public ICommand NextCommand { get; }
    public ICommand PrevCommand { get; }

    public ICommand ToMonthCommand { get; }
    public ICommand ToQuarterCommand { get; }
    public ICommand ToYearCommand { get; }
    public ICommand ToCustomCommand { get; }
    public ICommand ToAllCommand { get; }

    public Interaction<CustomPeriodViewModel, CustomPeriodViewModel?> ShowDialog { get; }

    public PeriodViewModel()
    {
        ShowDialog = new Interaction<CustomPeriodViewModel, CustomPeriodViewModel?>();

        NextCommand = ReactiveCommand.CreateFromTask(() =>
        {
            Shift(1);
            return Task.CompletedTask;
        });

        PrevCommand = ReactiveCommand.CreateFromTask(() =>
        {
            Shift(-1);
            return Task.CompletedTask;
        });

        ToMonthCommand = ReactiveCommand.CreateFromTask(() => 
        {
            From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            To = From.AddMonths(1).AddDays(-1);
            PeriodType = PeriodType.Month;
            this.RaisePropertyChanged(nameof(Text));

            return Task.CompletedTask;
        });

        ToQuarterCommand = ReactiveCommand.CreateFromTask(() =>
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
            this.RaisePropertyChanged(nameof(Text));

            return Task.CompletedTask;
        });

        ToYearCommand = ReactiveCommand.CreateFromTask(() =>
        {
            From = new DateTime(PeriodType != PeriodType.All ? To.Year : DateTime.Today.Year, 1, 1);
            To = new DateTime(PeriodType != PeriodType.All ? To.Year : DateTime.Today.Year, 12, 31);
            PeriodType = PeriodType.Year;
            this.RaisePropertyChanged(nameof(Text));

            return Task.CompletedTask;
        });

        ToCustomCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var period = new CustomPeriodViewModel 
            { 
                From = PeriodType != PeriodType.All ? this.From : DateTime.Today, 
                To = PeriodType != PeriodType.All ? this.To : DateTime.Today
            };

            var result = await ShowDialog.Handle(period);
            if (result != null)
            {
                PeriodType = PeriodType.Custom;
                From = result.From;
                To = result.To;
            }
            this.RaisePropertyChanged(nameof(Text));

            PeriodType = PeriodType.Custom;
        });

        ToAllCommand = ReactiveCommand.CreateFromTask(() =>
        {
            From = DateTime.MinValue;
            To = DateTime.MaxValue;
            PeriodType = PeriodType.All;
            this.RaisePropertyChanged(nameof(Text));

            return Task.CompletedTask;
        });
    }

    public PeriodType PeriodType
    {
        get => _periodType;
        set => this.RaiseAndSetIfChanged(ref _periodType, value);
    }

    public DateTime From
    {
        get => _from;
        set => this.RaiseAndSetIfChanged(ref _from, value);
    }

    public DateTime To
    {
        get => _to;
        set => this.RaiseAndSetIfChanged(ref _to, value);
    }

    public string Text
    {
        get
        {
            switch (_periodType) 
            {
                case PeriodType.Month:
                    return From.ToString("MMMM yyyy");
                case PeriodType.Quarter:
                    return From.ToString($"Квартал {(From.Month + 2) / 3} {From.Year}");
                case PeriodType.Year:
                    return From.ToString("yyyy");
                case PeriodType.All:
                    return From.ToString("Всё время");

                default: 
                    return From.ToString("dd.MM.yyyy") + " - " + To.ToString("dd.MM.yyyy");
            }
        }
    }

    private void Shift(int direction)
    {
        switch (_periodType)
        {
            case PeriodType.Month:
                From = From.AddMonths(direction);
                To = From.AddMonths(direction).AddDays(-1);
                break;
            case PeriodType.Quarter:
                From = From.AddMonths(direction * 3);
                To = From.AddMonths(3).AddDays(-1);
                break;
            case PeriodType.Year:
                From = From.AddYears(direction);
                To = To.AddYears(direction);
                break;
            case PeriodType.Custom:
                var diff = (To - From).Days;
                From = From.AddDays(direction * diff);
                To = To.AddDays(direction * diff);
                break;
        }

        this.RaisePropertyChanged(nameof(Text));
    }
}
