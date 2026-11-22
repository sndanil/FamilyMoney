using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Analytics;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.State;
using FamilyMoney.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FamilyMoney.ViewModels;

/// <summary>
/// Отчёт «Топ расходов»: свои фильтры (счёт, период, размер топа, объединение повторов),
/// карточки позиций и доля каждой от всех расходов периода.
/// </summary>
public partial class TopExpensesViewModel : ViewModelBase
{
    private static readonly DateTime HistoryFrom = new(1900, 1, 1);

    private static readonly DateTime HistoryTo = new(2100, 12, 31);

    private readonly IRepository _repository;
    private readonly AccountsViewModel _accounts;
    private readonly ILogger<TopExpensesViewModel> _logger;

    private bool _initialized;
    private bool _suppressScopeChange;
    private int _loadVersion;

    public TopExpensesViewModel(
        IRepository repository,
        IStateManager stateManager,
        AccountsViewModel accounts,
        ILogger<TopExpensesViewModel> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Период отчёта свой: по умолчанию текущий год и без записи в общее состояние.
        Period = new PeriodViewModel(stateManager ?? throw new ArgumentNullException(nameof(stateManager)))
        {
            UpdatesMainState = false,
        };
        Period.SetYear(DateTime.Today.Year);
        Period.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PeriodViewModel.Text) && _initialized)
            {
                Refresh();
            }
        };

        SubscribeMessages();
    }

    public PeriodViewModel Period { get; }

    public IReadOnlyList<int> TopOptions { get; } = [10, 20, 50, 100];

    public ObservableCollection<ReportAccount> Scopes { get; } = [];

    [ObservableProperty]
    public partial ReportAccount? SelectedScope { get; set; }

    [ObservableProperty]
    public partial int Top { get; set; } = 10;

    [ObservableProperty]
    public partial bool Merge { get; set; } = true;

    [ObservableProperty]
    public partial ObservableCollection<TopExpenseCardViewModel> Items { get; set; } = [];

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ShownText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? EmptyText { get; set; }

    public bool IsTop10 => Top == 10;

    public bool IsTop20 => Top == 20;

    public bool IsTop50 => Top == 50;

    public bool IsTop100 => Top == 100;

    /// <summary>Вход в раздел: обновляем список счетов и пересчитываем отчёт.</summary>
    public void Enter()
    {
        _initialized = true;
        LoadScopes();
        Refresh();
    }

    /// <summary>Полная перезагрузка — например, после смены базы данных.</summary>
    public void Reload() => Enter();

    [RelayCommand]
    private void SetTop10() => Top = 10;

    [RelayCommand]
    private void SetTop20() => Top = 20;

    [RelayCommand]
    private void SetTop50() => Top = 50;

    [RelayCommand]
    private void SetTop100() => Top = 100;

    partial void OnSelectedScopeChanged(ReportAccount? value)
    {
        if (_suppressScopeChange || !_initialized)
        {
            return;
        }

        Refresh();
    }

    partial void OnTopChanged(int value)
    {
        OnPropertyChanged(nameof(IsTop10));
        OnPropertyChanged(nameof(IsTop20));
        OnPropertyChanged(nameof(IsTop50));
        OnPropertyChanged(nameof(IsTop100));

        if (_initialized)
        {
            Refresh();
        }
    }

    partial void OnMergeChanged(bool value)
    {
        if (_initialized)
        {
            Refresh();
        }
    }

    private void LoadScopes()
    {
        var total = _accounts.Total;
        if (total.Children.Count == 0)
        {
            _accounts.LoadAccounts();
        }

        var previous = SelectedScope?.AccountId;

        _suppressScopeChange = true;
        try
        {
            Scopes.Clear();
            foreach (var scope in ReportAccountCatalog.Build(total))
            {
                Scopes.Add(scope);
            }

            SelectedScope = Scopes.FirstOrDefault(s => s.AccountId == previous) ?? Scopes[0];
        }
        finally
        {
            _suppressScopeChange = false;
        }
    }

    private void Refresh()
    {
        var version = ++_loadVersion;

        try
        {
            var from = ClampStart(Period.From);
            var to = ClampEnd(Period.To);
            if (to < from)
            {
                to = from;
            }

            var accounts = _repository.GetAccounts(includeDeleted: true).ToList();
            var accountIds = ReportScopeResolver.Resolve(accounts, SelectedScope?.AccountId);

            var transactions = accountIds.Count == 0
                ? new List<Transaction>()
                : _repository.GetTransactions(new TransactionsFilter
                {
                    PeriodFrom = from,
                    PeriodTo = to,
                }).ToList();

            var result = accountIds.Count == 0
                ? new TopExpensesResult([], 0m, 0, 0)
                : TopExpensesBuilder.Build(transactions, accountIds.ToHashSet(), from, to, Top, Merge);

            if (version != _loadVersion)
            {
                return;
            }

            Items = new ObservableCollection<TopExpenseCardViewModel>(BuildCards(result));

            SummaryText = result.Transactions == 0
                ? "Расходов за период нет"
                : $"Расходы {MoneyFormat.Full(result.Total)} · {result.Transactions} {Plural(result.Transactions, "операция", "операции", "операций")}";

            ShownText = result.Positions == 0
                ? string.Empty
                : $"показано {result.Items.Count} из {result.Positions}";

            EmptyText = result.Positions == 0 ? "За период нет расходов" : null;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось построить отчёт «Топ расходов»");

            if (version == _loadVersion)
            {
                Items = [];
                SummaryText = string.Empty;
                ShownText = string.Empty;
                EmptyText = "Не удалось построить отчёт";
            }
        }
    }

    private List<TopExpenseCardViewModel> BuildCards(TopExpensesResult result)
    {
        var categoryIds = result.Items.Where(i => i.CategoryId != null).Select(i => i.CategoryId!.Value).Distinct().ToList();
        var subCategoryIds = result.Items.Where(i => i.SubCategoryId != null).Select(i => i.SubCategoryId!.Value).Distinct().ToList();

        var categories = ResolveCategories(categoryIds);
        var subCategories = ResolveSubCategories(subCategoryIds);
        var images = categoryIds.ToDictionary(
            id => id,
            id => ImageDataHelper.ToByteArray(_repository.TryGetImage(id)));

        var cards = new List<TopExpenseCardViewModel>(result.Items.Count);

        foreach (var group in result.Items)
        {
            var category = group.CategoryId is { } categoryId && categories.TryGetValue(categoryId, out var foundCategory)
                ? foundCategory
                : null;

            var subCategory = group.SubCategoryId is { } subCategoryId && subCategories.TryGetValue(subCategoryId, out var foundSubCategory)
                ? foundSubCategory
                : null;

            cards.Add(new TopExpenseCardViewModel
            {
                CategoryName = category?.Name ?? "Без категории",
                SubCategoryName = subCategory?.Name ?? string.Empty,
                Comment = group.Comment ?? string.Empty,
                Sum = group.Sum,
                Percent = result.Total > 0m ? group.Sum / result.Total : 0m,
                Count = group.Count,
                ImageData = category is null ? null : images.GetValueOrDefault(category.Id),
                Icon = category?.Icon,
                IconColor = category?.IconColor,
            });
        }

        return cards;
    }

    private Dictionary<Guid, Category> ResolveCategories(IReadOnlyList<Guid> ids)
    {
        var result = _repository.GetCategories().ToDictionary(category => category.Id);

        // Категория могла быть удалена после операции — имя всё равно нужно.
        foreach (var id in ids.Where(id => !result.ContainsKey(id)))
        {
            if (_repository.GetCategory(id) is { } category)
            {
                result[id] = category;
            }
        }

        return result;
    }

    private Dictionary<Guid, SubCategory> ResolveSubCategories(IReadOnlyList<Guid> ids)
    {
        var result = _repository.GetSubCategories().ToDictionary(subCategory => subCategory.Id);

        foreach (var id in ids.Where(id => !result.ContainsKey(id)))
        {
            if (_repository.GetSubCategory(id) is { } subCategory)
            {
                result[id] = subCategory;
            }
        }

        return result;
    }

    private static DateTime ClampStart(DateTime value)
    {
        if (value.Year >= HistoryFrom.Year)
        {
            return value;
        }

        return HistoryFrom;
    }

    private static DateTime ClampEnd(DateTime value)
    {
        if (value.Year <= HistoryTo.Year)
        {
            return value;
        }

        return HistoryTo;
    }

    private static string Plural(int count, string one, string few, string many)
    {
        var mod100 = Math.Abs(count) % 100;
        var mod10 = mod100 % 10;

        if (mod100 is >= 11 and <= 14)
        {
            return many;
        }

        return mod10 switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many,
        };
    }

    private void SubscribeMessages()
    {
        WeakReferenceMessenger.Default.Register<TopExpensesViewModel, TransactionChangedMessage>(this, (vm, message) =>
        {
            if (vm._initialized)
            {
                vm.Refresh();
            }
        });
    }
}
