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
/// Отчёт «Топ»: свои фильтры (счёт, период, размер топа, объединение повторов),
/// карточки позиций и доля каждой от всех операций периода нужного вида.
/// Наследники задают вид — расходы или доходы.
/// </summary>
public partial class TopOperationsViewModel : ViewModelBase
{
    private static readonly DateTime HistoryFrom = new(1900, 1, 1);

    private static readonly DateTime HistoryTo = new(2100, 12, 31);

    private readonly IRepository _repository;
    private readonly AccountsViewModel _accounts;
    private readonly ILogger<TopOperationsViewModel> _logger;
    private readonly TopOperationKind _kind;

    private bool _initialized;
    private bool _suppressScopeChange;
    private int _loadVersion;

    // Кэш последнего пересчёта: операции выбранной области и за период, и оформление счетов.
    // Панель выбранной карточки строится по нему, без нового запроса в базу.
    private List<Transaction> _transactions = [];
    private Dictionary<Guid, AccountVisual> _accountVisuals = [];
    private OperationKey? _selectedKey;

    public event EventHandler? DetailsVisibilityChanged;

    protected TopOperationsViewModel(
        IRepository repository,
        IStateManager stateManager,
        AccountsViewModel accounts,
        ILogger<TopOperationsViewModel> logger,
        TopOperationKind kind)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kind = kind;

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

    /// <summary>
    /// Объединять только по категории и подкатегории, не учитывая комментарий.
    /// Имеет смысл только вместе с <see cref="Merge"/>.
    /// </summary>
    [ObservableProperty]
    public partial bool MergeWithoutComments { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<TopOperationCardViewModel> Items { get; set; } = [];

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? EmptyText { get; set; }

    [ObservableProperty]
    public partial TopOperationCardViewModel? SelectedCard { get; set; }

    [ObservableProperty]
    public partial bool IsDetailsOpen { get; set; }

    [ObservableProperty]
    public partial string DetailsCategory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailsSubCategory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDetailsSubCategory { get; set; }

    [ObservableProperty]
    public partial string DetailsComment { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDetailsComment { get; set; }

    [ObservableProperty]
    public partial string DetailsTotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailsMetaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial byte[]? DetailsImageData { get; set; }

    [ObservableProperty]
    public partial string? DetailsIcon { get; set; }

    [ObservableProperty]
    public partial string? DetailsIconColor { get; set; }

    /// <summary>Дни и операции выбранной карточки.</summary>
    public ObservableCollection<OperationDayGroupViewModel> Details { get; } = [];

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

    /// <summary>
    /// Клик по карточке: выбрать её и показать операции справа. Повторный клик по выбранной
    /// карточке закрывает панель.
    /// </summary>
    public void SelectCard(TopOperationCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (ReferenceEquals(SelectedCard, card))
        {
            CloseDetails();
            return;
        }

        if (SelectedCard != null)
        {
            SelectedCard.IsSelected = false;
        }

        SelectedCard = card;
        card.IsSelected = true;
        _selectedKey = KeyOf(card);

        BuildDetails(card);
        IsDetailsOpen = true;
    }

    public void CloseDetails()
    {
        if (SelectedCard != null)
        {
            SelectedCard.IsSelected = false;
        }

        SelectedCard = null;
        _selectedKey = null;

        // Содержимое панели не чистим: она ещё плавно сворачивается, и пустой список
        // во время анимации выглядел бы как рывок. Новая карточка заменит данные.
        IsDetailsOpen = false;
    }

    [RelayCommand]
    private void ClosePanel() => CloseDetails();

    partial void OnIsDetailsOpenChanged(bool value)
    {
        DetailsVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

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

    partial void OnMergeWithoutCommentsChanged(bool value)
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

            var ids = accountIds.ToHashSet();
            var scoped = accountIds.Count == 0
                ? new List<Transaction>()
                : transactions.Where(t => t.AccountId is { } accountId && ids.Contains(accountId)).ToList();

            var result = accountIds.Count == 0
                ? new TopOperationsResult([], 0m, 0, 0)
                : TopOperationsBuilder.Build(scoped, ids, from, to, Top, Merge, IgnoresComments, _kind);

            if (version != _loadVersion)
            {
                return;
            }

            _transactions = scoped;
            _accountVisuals = accounts.ToDictionary(
                account => account.Id,
                account => new AccountVisual(
                    account.Name,
                    ImageDataHelper.ToByteArray(_repository.TryGetImage(account.Id)),
                    account.Icon,
                    account.IconColor));

            Items = new ObservableCollection<TopOperationCardViewModel>(BuildCards(result));
            RestoreSelection();

            SummaryText = result.Transactions == 0
                ? $"{(IsIncome ? "Доходов" : "Расходов")} за период нет"
                : BuildSummary(result);

            EmptyText = result.Positions == 0
                ? $"За период нет {(IsIncome ? "доходов" : "расходов")}"
                : null;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось построить отчёт «{Report}»", Title);

            if (version == _loadVersion)
            {
                Items = [];
                SummaryText = string.Empty;
                EmptyText = "Не удалось построить отчёт";
            }
        }
    }

    /// <summary>После пересчёта возвращаем выделение на ту же позицию, если она осталась в топе.</summary>
    private void RestoreSelection()
    {
        if (_selectedKey is not { } key)
        {
            return;
        }

        var card = Items.FirstOrDefault(candidate => KeyOf(candidate) == key);
        if (card == null)
        {
            CloseDetails();
            return;
        }

        card.IsSelected = true;
        SelectedCard = card;
        BuildDetails(card);
    }

    private void BuildDetails(TopOperationCardViewModel card)
    {
        var operations = TopOperations.Select(
            _transactions,
            card.CategoryId,
            card.SubCategoryId,
            card.Comment,
            _kind,
            ignoreComment: IgnoresComments);
        var days = TopOperations.GroupByDay(operations);

        // Комментарий в карточке есть только тогда, когда по нему шла группировка;
        // иначе он принадлежит конкретной операции и показывается в строке панели.
        var showRowComments = string.IsNullOrEmpty(card.Comment);

        Details.Clear();
        foreach (var day in days)
        {
            Details.Add(new OperationDayGroupViewModel
            {
                Date = day.Date,
                Sum = day.Sum,
                Items = day.Items.Select(item => ToRow(item, showRowComments)).ToList(),
            });
        }

        DetailsCategory = card.CategoryName;
        DetailsSubCategory = card.SubCategoryName;
        HasDetailsSubCategory = !string.IsNullOrEmpty(card.SubCategoryName);
        DetailsComment = card.Comment;
        HasDetailsComment = !string.IsNullOrEmpty(card.Comment);
        DetailsImageData = card.ImageData;
        DetailsIcon = card.Icon;
        DetailsIconColor = card.IconColor;
        DetailsTotalText = $"{MoneyFormat.Full(card.Sum)} · {card.Percent:P0}";
        DetailsMetaText =
            $"{operations.Count} {Plural(operations.Count, "операция", "операции", "операций")} · " +
            $"{days.Count} {Plural(days.Count, "день", "дня", "дней")}";
    }

    private OperationRowViewModel ToRow(Transaction transaction, bool showComment)
    {
        var account = transaction.AccountId is { } accountId && _accountVisuals.TryGetValue(accountId, out var visual)
            ? visual
            : default;

        return new OperationRowViewModel
        {
            Comment = showComment ? transaction.Comment ?? string.Empty : string.Empty,
            ShowComment = showComment && !string.IsNullOrWhiteSpace(transaction.Comment),
            Tags = (transaction.Tags ?? [])
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .ToList(),
            Sum = transaction.Sum,
            AccountName = account.Name ?? string.Empty,
            ImageData = account.ImageData,
            Icon = account.Icon,
            IconColor = account.IconColor,
        };
    }

    private static OperationKey KeyOf(TopOperationCardViewModel card)
    {
        return new OperationKey(card.CategoryId, card.SubCategoryId, CommentNormalizer.Normalize(card.Comment));
    }

    private readonly record struct OperationKey(Guid? CategoryId, Guid? SubCategoryId, string Comment);

    /// <summary>Имя и оформление счёта для строки панели.</summary>
    private readonly record struct AccountVisual(string Name, byte[]? ImageData, string? Icon, string? IconColor);

    /// <summary>Вид отчёта: доходы.</summary>
    protected bool IsIncome => _kind == TopOperationKind.Income;

    /// <summary>Комментарий не участвует в объединении — только вместе с самим объединением.</summary>
    private bool IgnoresComments => Merge && MergeWithoutComments;

    /// <summary>Название отчёта для документов и журнала.</summary>
    protected string Title => IsIncome ? "Топ доходов" : "Топ расходов";

    /// <summary>Строка сводки: сумма, число операций и сколько позиций показано из всех.</summary>
    private string BuildSummary(TopOperationsResult result)
    {
        var text = $"{(IsIncome ? "Доходы" : "Расходы")} {MoneyFormat.Full(result.Total)} · " +
                   $"{result.Transactions} {Plural(result.Transactions, "операция", "операции", "операций")}";

        return result.Positions == 0
            ? text
            : $"{text} · показано {result.Items.Count} из {result.Positions}";
    }

    private List<TopOperationCardViewModel> BuildCards(TopOperationsResult result)
    {
        var categoryIds = result.Items.Where(i => i.CategoryId != null).Select(i => i.CategoryId!.Value).Distinct().ToList();
        var subCategoryIds = result.Items.Where(i => i.SubCategoryId != null).Select(i => i.SubCategoryId!.Value).Distinct().ToList();

        var categories = ResolveCategories(categoryIds);
        var subCategories = ResolveSubCategories(subCategoryIds);
        var images = categoryIds.ToDictionary(
            id => id,
            id => ImageDataHelper.ToByteArray(_repository.TryGetImage(id)));

        var cards = new List<TopOperationCardViewModel>(result.Items.Count);

        foreach (var group in result.Items)
        {
            var category = group.CategoryId is { } categoryId && categories.TryGetValue(categoryId, out var foundCategory)
                ? foundCategory
                : null;

            var subCategory = group.SubCategoryId is { } subCategoryId && subCategories.TryGetValue(subCategoryId, out var foundSubCategory)
                ? foundSubCategory
                : null;

            cards.Add(new TopOperationCardViewModel
            {
                CategoryId = group.CategoryId,
                SubCategoryId = group.SubCategoryId,
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
        WeakReferenceMessenger.Default.Register<TopOperationsViewModel, TransactionChangedMessage>(this, (vm, message) =>
        {
            if (vm._initialized)
            {
                vm.Refresh();
            }
        });
    }
}
