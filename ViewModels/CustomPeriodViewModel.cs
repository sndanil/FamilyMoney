using ReactiveUI;
using System;
using System.Reactive;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public sealed class CustomPeriodViewModel: ViewModelBase
{
    private DateTime _from;
    private DateTime _to;

    public ReactiveCommand<Unit, CustomPeriodViewModel?> OkCommand { get; }

    public ReactiveCommand<Unit, CustomPeriodViewModel?> CancelCommand { get; }

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

    public CustomPeriodViewModel()
    {
        OkCommand = ReactiveCommand.Create(() =>
        {
            return (CustomPeriodViewModel?)this;
        });

        CancelCommand = ReactiveCommand.Create(() =>
        {
            return (CustomPeriodViewModel?)null;
        });
    }
}
