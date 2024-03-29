﻿using ReactiveUI;
using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public enum PeriodType
{
    Custom,
    Month,
    Quater,
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
    public ICommand ToQuaterCommand { get; }
    public ICommand ToYearCommand { get; }
    public ICommand ToCustomCommand { get; }
    public ICommand ToAllCommand { get; }

    public PeriodViewModel()
    {
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
            PeriodType = PeriodType.Month;
            return Task.CompletedTask;
        });

        ToQuaterCommand = ReactiveCommand.CreateFromTask(() =>
        {
            PeriodType = PeriodType.Quater;
            return Task.CompletedTask;
        });

        ToYearCommand = ReactiveCommand.CreateFromTask(() =>
        {
            PeriodType = PeriodType.Year;
            return Task.CompletedTask;
        });

        ToCustomCommand = ReactiveCommand.CreateFromTask(() =>
        {
            PeriodType = PeriodType.Custom;
            return Task.CompletedTask;
        });

        ToAllCommand = ReactiveCommand.CreateFromTask(() =>
        {
            PeriodType = PeriodType.All;
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
                To = To.AddMonths(direction);
                break;
        }

        this.RaisePropertyChanged(nameof(Text));
    }
}
