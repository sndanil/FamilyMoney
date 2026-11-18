using System;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Controls;

public enum PeriodPickerMode
{
    Day,
    Month,
    Quarter,
    Year,
}

public sealed class PeriodPickerCell
{
    public string Label { get; init; } = string.Empty;

    public DateTime? Date { get; init; }

    public int Year { get; init; }

    public int Month { get; init; }

    public int Quarter { get; init; }

    public bool IsEmpty { get; init; }

    public bool IsCurrent { get; init; }
}

/// <summary>
/// Выбор периода на мобильном экране: день, месяц, квартал или год.
/// Отличается от десктопного меню тем, что не предлагает произвольный интервал и «всё время».
/// </summary>
public partial class PeriodPickerViewModel : ViewModelBase
{
    private readonly PeriodViewModel _period;

    private int _year;
    private int _month;

    public PeriodPickerViewModel(PeriodViewModel period)
    {
        _period = period ?? throw new ArgumentNullException(nameof(period));
        Reset();
    }

    /// <summary>Период выбран; страница закрывает поповер.</summary>
    public event EventHandler? Applied;

    [ObservableProperty]
    public partial PeriodPickerMode Mode { get; set; }

    public ObservableCollection<PeriodPickerCell> Cells { get; } = [];

    public string Header { get; private set; } = string.Empty;

    public int Columns { get; private set; } = 4;

    public bool IsDayMode => Mode == PeriodPickerMode.Day;

    public bool IsMonthMode => Mode == PeriodPickerMode.Month;

    public bool IsQuarterMode => Mode == PeriodPickerMode.Quarter;

    public bool IsYearMode => Mode == PeriodPickerMode.Year;

    /// <summary>Показывает период, который сейчас выбран в приложении.</summary>
    public void Reset()
    {
        var from = _period.PeriodType == PeriodType.All ? DateTime.Today : _period.From;

        _year = from.Year;
        _month = from.Month;

        SetMode(_period.PeriodType switch
        {
            PeriodType.Day => PeriodPickerMode.Day,
            PeriodType.Quarter => PeriodPickerMode.Quarter,
            PeriodType.Year => PeriodPickerMode.Year,
            _ => PeriodPickerMode.Month,
        });
    }

    [RelayCommand]
    private void SetDayMode() => SetMode(PeriodPickerMode.Day);

    [RelayCommand]
    private void SetMonthMode() => SetMode(PeriodPickerMode.Month);

    [RelayCommand]
    private void SetQuarterMode() => SetMode(PeriodPickerMode.Quarter);

    [RelayCommand]
    private void SetYearMode() => SetMode(PeriodPickerMode.Year);

    [RelayCommand]
    private void Prev() => Shift(-1);

    [RelayCommand]
    private void Next() => Shift(1);

    [RelayCommand]
    private void Today()
    {
        var today = DateTime.Today;
        _year = today.Year;
        _month = today.Month;

        switch (Mode)
        {
            case PeriodPickerMode.Day:
                _period.SetDay(today);
                break;
            case PeriodPickerMode.Quarter:
                _period.SetQuarter(today.Year, ((today.Month - 1) / 3) + 1);
                break;
            case PeriodPickerMode.Year:
                _period.SetYear(today.Year);
                break;
            default:
                _period.SetMonth(today.Year, today.Month);
                break;
        }

        Applied?.Invoke(this, EventArgs.Empty);
    }

    public void SelectCell(PeriodPickerCell cell)
    {
        if (cell.IsEmpty)
        {
            return;
        }

        switch (Mode)
        {
            case PeriodPickerMode.Day when cell.Date is DateTime date:
                _period.SetDay(date);
                break;
            case PeriodPickerMode.Month:
                _period.SetMonth(cell.Year, cell.Month);
                break;
            case PeriodPickerMode.Quarter:
                _period.SetQuarter(cell.Year, cell.Quarter);
                break;
            case PeriodPickerMode.Year:
                _period.SetYear(cell.Year);
                break;
            default:
                return;
        }

        Applied?.Invoke(this, EventArgs.Empty);
    }

    private void SetMode(PeriodPickerMode mode)
    {
        Mode = mode;

        OnPropertyChanged(nameof(IsDayMode));
        OnPropertyChanged(nameof(IsMonthMode));
        OnPropertyChanged(nameof(IsQuarterMode));
        OnPropertyChanged(nameof(IsYearMode));

        Rebuild();
    }

    private void Shift(int direction)
    {
        switch (Mode)
        {
            case PeriodPickerMode.Day:
                var month = new DateTime(_year, _month, 1).AddMonths(direction);
                _year = month.Year;
                _month = month.Month;
                break;
            case PeriodPickerMode.Year:
                _year += direction * 9;
                break;
            default:
                _year += direction;
                break;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        Cells.Clear();

        switch (Mode)
        {
            case PeriodPickerMode.Day:
                Columns = 7;
                Header = new DateTime(_year, _month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture);
                BuildDays();
                break;
            case PeriodPickerMode.Month:
                Columns = 4;
                Header = _year.ToString(CultureInfo.CurrentCulture);
                BuildMonths();
                break;
            case PeriodPickerMode.Quarter:
                Columns = 4;
                Header = _year.ToString(CultureInfo.CurrentCulture);
                BuildQuarters();
                break;
            default:
                Columns = 3;
                Header = $"{_year - 4} – {_year + 4}";
                BuildYears();
                break;
        }

        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Columns));
    }

    private void BuildDays()
    {
        var first = new DateTime(_year, _month, 1);
        var daysInMonth = DateTime.DaysInMonth(_year, _month);
        // Понедельник — первый столбец.
        var offset = ((int)first.DayOfWeek + 6) % 7;

        for (var i = 0; i < offset; i++)
        {
            Cells.Add(new PeriodPickerCell { IsEmpty = true });
        }

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(_year, _month, day);
            Cells.Add(new PeriodPickerCell
            {
                Label = day.ToString(CultureInfo.CurrentCulture),
                Date = date,
                Year = _year,
                Month = _month,
                IsCurrent = IsCurrentDay(date),
            });
        }

        while (Cells.Count % 7 != 0)
        {
            Cells.Add(new PeriodPickerCell { IsEmpty = true });
        }
    }

    private void BuildMonths()
    {
        var format = CultureInfo.CurrentCulture.DateTimeFormat;

        for (var month = 1; month <= 12; month++)
        {
            Cells.Add(new PeriodPickerCell
            {
                Label = Capitalize(format.GetAbbreviatedMonthName(month)),
                Year = _year,
                Month = month,
                IsCurrent = IsCurrentMonth(month),
            });
        }
    }

    private void BuildQuarters()
    {
        for (var quarter = 1; quarter <= 4; quarter++)
        {
            Cells.Add(new PeriodPickerCell
            {
                Label = $"{quarter} кв.",
                Year = _year,
                Quarter = quarter,
                IsCurrent = IsCurrentQuarter(quarter),
            });
        }
    }

    private void BuildYears()
    {
        for (var year = _year - 4; year <= _year + 4; year++)
        {
            Cells.Add(new PeriodPickerCell
            {
                Label = year.ToString(CultureInfo.CurrentCulture),
                Year = year,
                IsCurrent = IsCurrentYear(year),
            });
        }
    }

    private bool IsCurrentDay(DateTime date)
    {
        return _period.PeriodType == PeriodType.Day && _period.From.Date == date.Date;
    }

    private bool IsCurrentMonth(int month)
    {
        return _period.PeriodType == PeriodType.Month
            && _period.From.Year == _year
            && _period.From.Month == month;
    }

    private bool IsCurrentQuarter(int quarter)
    {
        return _period.PeriodType == PeriodType.Quarter
            && _period.From.Year == _year
            && ((_period.From.Month - 1) / 3) + 1 == quarter;
    }

    private bool IsCurrentYear(int year)
    {
        return _period.PeriodType == PeriodType.Year && _period.From.Year == year;
    }

    private static string Capitalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return char.ToUpper(value[0], CultureInfo.CurrentCulture) + value[1..];
    }
}
