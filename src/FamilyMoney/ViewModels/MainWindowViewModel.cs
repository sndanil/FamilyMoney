﻿using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using FamilyMoney.DataAccess;
using FamilyMoney.Import;
using FamilyMoney.State;
using ReactiveUI;
using System.Linq;
using System.Reactive.Concurrency;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IStateManager _stateManager;
    private readonly IRepository _repository;
    private readonly IImporter _importer;

    private int _leftSideWidth = 400;
    private bool _isPaneOpen = false;
    private Control? _currentPanel;

    private readonly PeriodViewModel _period;
    private readonly CategoriesViewModel _categoriesViewModel;
    private readonly AccountsViewModel _accountsViewModel;
    private readonly TransactionsViewModel _transactionsViewModel;

    public ICommand TriggerPaneCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand SwitchToCommand { get; }

    public int LeftSideWidth
    {
        get => _leftSideWidth;
        set => this.RaiseAndSetIfChanged(ref _leftSideWidth, value);
    }

    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        set => this.RaiseAndSetIfChanged(ref _isPaneOpen, value);
    }

    public Control? CurrentPanel
    {
        get => _currentPanel;
        set => this.RaiseAndSetIfChanged(ref _currentPanel, value);
    }

    public PeriodViewModel Period
    {
        get => _period;
    }

    public CategoriesViewModel Categories
    {
        get => _categoriesViewModel;
    }

    public AccountsViewModel Accounts
    {
        get => _accountsViewModel;
    }

    public TransactionsViewModel Transactions
    {
        get => _transactionsViewModel;
    }

    public MainWindowViewModel(IRepository repository, 
        IStateManager stateManager,
        IImporter importer,
        PeriodViewModel period, 
        CategoriesViewModel categoriesViewModel,
        AccountsViewModel accounts, 
        TransactionsViewModel transactionsViewModel)
    {
        _repository = repository;
        _importer = importer;
        _stateManager = stateManager;        

        _categoriesViewModel = categoriesViewModel;
        _accountsViewModel = accounts;
        _transactionsViewModel = transactionsViewModel;
        _period = period;

        TriggerPaneCommand = ReactiveCommand.Create(() => IsPaneOpen = !IsPaneOpen);

        ImportCommand = ReactiveCommand.Create(async (Avalonia.Visual visual) =>
        {
            var topLevel = TopLevel.GetTopLevel(visual);
            var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выбор файла для импорта",
                AllowMultiple = false,
                FileTypeFilter = [new("Файл CSV") { Patterns = ["*.csv"], MimeTypes = ["*/*"] }, FilePickerFileTypes.All]
            });

            if (files.Any())
            {
                await using var stream = await files.Single().OpenReadAsync();
                _importer.DoImport(_repository, stream);
                MainInit();
            }
        });

        SwitchToCommand = ReactiveCommand.Create((Control control) =>
        {
            CurrentPanel = control;
        });

        RxApp.MainThreadScheduler.Schedule(MainInit);
    }

    private void MainInit()
    {
        _accountsViewModel.LoadAccounts();
        var state = _stateManager.GetMainState();
        state.PeriodFrom = _period.From;
        state.PeriodTo = _period.To;
        state.Accounts = _accountsViewModel.Total.Children.ToArray();
        _stateManager.SetMainState(state);
    }
}
