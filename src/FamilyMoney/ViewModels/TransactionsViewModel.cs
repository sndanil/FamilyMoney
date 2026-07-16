using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.State;
using FamilyMoney.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public partial class TransactionsViewModel : ViewModelBase
{
    private readonly IRepository _repository;
    private readonly IStateManager _stateManager;
    private readonly IGlobalConfiguration _configuration;

    private DateTime _lastTransactionDate = DateTime.Today;

    private readonly HashSet<Guid> _openedNodes = [];

    private readonly Dictionary<Guid, BaseCategoryViewModel> _categoriesCache = [];
    private readonly Dictionary<Guid, BaseSubCategoryViewModel> _subCategoriesCache = [];

    // Кэш готовых списков для страницы/окна редактирования транзакции: их построение
    // требует нескольких проходов по базе и выполняется заметно долго на Android.
    // Доступ под блокировкой — списки строятся в фоновом потоке (Task.Run).
    private readonly object _listsCacheLock = new();
    private readonly Dictionary<Type, List<BaseCategoryViewModel>> _categoryListsCache = [];
    private readonly Dictionary<Type, List<BaseSubCategoryViewModel>> _subCategoryListsCache = [];

    private readonly ObservableCollection<BaseTransactionsGroupViewModel> _selectedTransactionGroups = [];

    [ObservableProperty]
    public partial SummaryTransactionsGroup DebetTransactions { get; set; } = new SummaryTransactionsGroup();

    [ObservableProperty]
    public partial SummaryTransactionsGroup CreditTransactions { get; set; } = new SummaryTransactionsGroup();

    [ObservableProperty]
    public partial SummaryTransactionsGroup TransferTransactions { get; set; } = new SummaryTransactionsGroup();

    [ObservableProperty]
    public partial ObservableCollection<TransactionsByDatesGroup> TransactionsDyDates { get; set; } = [];

    [ObservableProperty]
    public partial decimal Total { get; set; }

    [ObservableProperty]
    public partial bool EnableMultiSelect { get; set; }

    [ObservableProperty]
    public partial decimal MultiSelectTotal { get; set; }

    [ObservableProperty]
    public partial BaseTransactionsGroupViewModel? SelectedTransactionGroup { get; set; }

    [ObservableProperty]
    public partial string FilterCommentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFilterActive { get; set; }

    public ObservableCollection<FilterTagViewModel> FilterTags { get; } = [];

    // Применённый фильтр хранится отдельно от полей ввода, чтобы набор текста
    // и клики по тегам не перефильтровывали список до нажатия «Применить».
    private string[] _appliedCommentParts = [];
    private readonly HashSet<string> _appliedTags = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<BaseTransactionsGroupViewModel> SelectedTransactionGroups
    {
        get => _selectedTransactionGroups;
    }

    public TransactionsViewModel(IRepository repository, IStateManager stateManager, IGlobalConfiguration configuration)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        SubscribeMessages();

        var transactionsFilter = new Func<BaseTransactionsGroupViewModel, bool>((t) => 
        {
            if (t is TransactionRowViewModel row)
            {
                return !(SelectedTransactionGroups.OfType<CategoryTransactionsGroupViewModel>().Any(ct => ct.Category?.Id == row.Category?.Id)
                    || SelectedTransactionGroups.OfType<SubCategoryTransactionsGroupViewModel>().Any(ct => ct.SubCategory?.Id == row.SubCategory?.Id));
            }

            if (t is SubCategoryTransactionsGroupViewModel subCategory)
            {
                return !SelectedTransactionGroups.OfType<CategoryTransactionsGroupViewModel>()
                    .Any(ct => ct.SubCategories.Any(s => s.SubCategory?.Id == subCategory.SubCategory?.Id));
            }

            return true;
        });
        SelectedTransactionGroups.CollectionChanged += (sender, e) => MultiSelectTotal = SelectedTransactionGroups
                                                                                            .Where(transactionsFilter)
                                                                                            .Sum(g => g.IsDebet ? g.Sum : -g.Sum);
    }

    [RelayCommand]
    public async Task ToggleExpandAsync(BaseTransactionsGroupViewModel child)
    {
        child.IsExpanded = !child.IsExpanded;
        var id = ((child as CategoryTransactionsGroupViewModel)?.Category?.Id
                ?? (child as SubCategoryTransactionsGroupViewModel)?.SubCategory?.Id)
                ?? Guid.Empty;
        if (child.IsExpanded)
        {
            _openedNodes.Add(id);
        }
        else
        {
            _openedNodes.Remove(id);
        }
    }

    [RelayCommand]
    public async Task SelectAsync(BaseTransactionsGroupViewModel child)
    {
        if (!EnableMultiSelect && (SelectedTransactionGroups.Count > 1 || child != SelectedTransactionGroup))
            await ClearSelectionAsync();

        child.IsSelected = !child.IsSelected;
        if (child.IsSelected)
        {
            SelectedTransactionGroups.Add(child);
        }
        else
        {
            SelectedTransactionGroups.Remove(child);
        }

        SelectedTransactionGroup = SelectedTransactionGroups.LastOrDefault();
    }

    [RelayCommand]
    public async Task EditAsync(TransactionRowViewModel child)
    {
        await EditTransaction(child);
    }

    [RelayCommand]
    public async Task CopyAsync(TransactionRowViewModel child)
    {
        await CopyTransaction(child);
    }

    private void SubscribeMessages()
    {
        WeakReferenceMessenger.Default.Register<TransactionsViewModel, MainStateChangedMessage>(this, async (t, m) =>
        {
            if (m?.State != null)
            {
                await RefreshTransactions(m.State);
            }
        });

        WeakReferenceMessenger.Default.Register<TransactionsViewModel, CategoryUpdateMessage>(this, async (t, m) =>
        {
            if (m?.CategoryId != null)
            {
                lock (t._listsCacheLock)
                {
                    t._categoriesCache.Remove(m.CategoryId.GetValueOrDefault());
                }
            }

            t.InvalidateListsCache(categories: true, subCategories: true);
        });

        WeakReferenceMessenger.Default.Register<TransactionsViewModel, TransactionChangedMessage>(this, async (t, _) =>
        {
            // Суммы, комментарии и теги подкатегорий выводятся из транзакций.
            t.InvalidateListsCache(categories: false, subCategories: true);
            await Task.CompletedTask;
        });

        WeakReferenceMessenger.Default.Register<TransactionsViewModel, DatabaseChangedMessage>(this, async (t, _) =>
        {
            lock (t._listsCacheLock)
            {
                t._categoriesCache.Clear();
                t._subCategoriesCache.Clear();
            }

            t.InvalidateListsCache(categories: true, subCategories: true);
            t._openedNodes.Clear();
            await Task.CompletedTask;
        });
    }

    [RelayCommand]
    public async Task AddTransferAsync()
    {
        // Сбор данных для формы — тяжёлая работа с базой, выполняем вне UI-потока.
        var transactionViewModel = await Task.Run(() =>
        {
            var viewModel = CreateNewTransaction<TransferTransactionViewModel>(
                GetCategories<TransferCategory, TransferCategoryViewModel>(),
                GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel, TransferCategoryViewModel>(),
                null
                );
            viewModel.IsTransfer = true;
            return viewModel;
        });

        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<BaseTransactionViewModel>(transactionViewModel));
        if (result != null)
        {
            await SaveTransaction(new TransferTransaction { Id = Guid.NewGuid() }, result);
        }
    }

    [RelayCommand]
    public async Task AddCreditAsync()
    {
        var transactionViewModel = await Task.Run(() => CreateNewTransaction<CreditTransactionViewModel>(
            GetCategories<CreditCategory, CreditCategoryViewModel>(),
            GetSubCategories<CreditSubCategory, CreditSubCategoryViewModel, CreditCategoryViewModel>(),
            null
            ));

        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<BaseTransactionViewModel>(transactionViewModel));
        if (result != null)
        {
            await SaveTransaction(new CreditTransaction { Id = Guid.NewGuid() }, result);
        }
    }

    [RelayCommand]
    public async Task AddDebetAsync()
    {
        var transactionViewModel = await Task.Run(() => CreateNewTransaction<DebetTransactionViewModel>(
            GetCategories<DebetCategory, DebetCategoryViewModel>(),
            GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel, DebetCategoryViewModel>(),
            null
            ));

        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<BaseTransactionViewModel>(transactionViewModel));
        if (result != null)
        {
            await SaveTransaction(new DebetTransaction { Id = Guid.NewGuid() }, result);
        }
    }

    [RelayCommand]
    public async Task DeleteAsync(TransactionRowViewModel transactionGroupViewModel)
    {
        var transaction = _repository.GetTransaction(transactionGroupViewModel.Id);
        _repository.DeleteTransaction(transactionGroupViewModel.Id);
        await SendTransactionChanged(transaction, null);
        await RefreshTransactions(_stateManager.GetMainState());
    }

    private async Task CopyTransaction(TransactionRowViewModel transactionGroupViewModel)
    {
        // Сбор данных для формы — тяжёлая работа с базой, выполняем вне UI-потока.
        var (transaction, transactionViewModel) = await Task.Run<(Transaction?, BaseTransactionViewModel?)>(() =>
        {
            var entity = _repository.GetTransaction(transactionGroupViewModel.Id);

            BaseTransactionViewModel? viewModel;
            if (entity is DebetTransaction)
            {
                viewModel = CreateNewTransaction<DebetTransactionViewModel>(
                    GetCategories<DebetCategory, DebetCategoryViewModel>(),
                    GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel, DebetCategoryViewModel>(),
                    transactionGroupViewModel
                    );
            }
            else if (entity is CreditTransaction)
            {
                viewModel = CreateNewTransaction<CreditTransactionViewModel>(
                    GetCategories<CreditCategory, CreditCategoryViewModel>(),
                    GetSubCategories<CreditSubCategory, CreditSubCategoryViewModel, CreditCategoryViewModel>(),
                    transactionGroupViewModel
                    );
            }
            else if (entity is TransferTransaction)
            {
                viewModel = CreateNewTransaction<TransferTransactionViewModel>(
                    GetCategories<TransferCategory, TransferCategoryViewModel>(),
                    GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel, TransferCategoryViewModel>(),
                    transactionGroupViewModel
                    );
                viewModel.IsTransfer = true;
            }
            else
            {
                return (null, null);
            }

            viewModel.Sum = entity!.Sum;
            viewModel.Comment = entity.Comment;

            return (entity, viewModel);
        });

        if (transaction == null || transactionViewModel == null)
        {
            return;
        }

        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<BaseTransactionViewModel>(transactionViewModel));
        if (result != null)
        {
            transaction.Id = Guid.NewGuid();
            await SaveTransaction(transaction, result);
        }
    }

    private static async Task SendTransactionChanged(Transaction? before, Transaction? after)
    {
        WeakReferenceMessenger.Default.Send(new TransactionChangedMessage(before, after));
    }

    private BaseTransactionViewModel CreateNewTransaction<T>(IList<BaseCategoryViewModel> categories, 
                                                            IList<BaseSubCategoryViewModel> subCategories,
                                                            BaseTransactionsGroupViewModel? byGroup) 
        where T : BaseTransactionViewModel, new()
    {
        var state = _stateManager.GetMainState();
        var flatAccounts = state.FlatAccounts.Where(a => !a.IsHidden && a.Parent?.IsHidden == false).ToList();
        var account = state.SelectedAccountId.HasValue ?
                            flatAccounts.FirstOrDefault(a => !a.IsGroup && (a.Id == state.SelectedAccountId || a.Parent?.Id == state.SelectedAccountId))
                            : flatAccounts.FirstOrDefault(a => !a.IsGroup);

        var transactionViewModel = new T()
        {
            FlatAccounts = flatAccounts.Where(a => !a.IsHidden).ToList(),
            Account = account,
            Categories = categories.Where(c => !c.IsHidden).ToList(),
            SubCategories = subCategories.Where(c => c.Category?.IsHidden == false).ToList(),
        };

        if (byGroup is CategoryTransactionsGroupViewModel categoryGroupViewModel)
        {
            transactionViewModel.Category = categories.FirstOrDefault(c => c.Id == categoryGroupViewModel.Category?.Id);
        }

        if (byGroup is SubCategoryTransactionsGroupViewModel subCategoryGroupViewModel)
        {
            transactionViewModel.Category = categories.FirstOrDefault(c => c.Id == subCategoryGroupViewModel.SubCategory?.CategoryId);
            transactionViewModel.SubCategory = transactionViewModel.SubCategories.FirstOrDefault(s => s.Id == subCategoryGroupViewModel?.SubCategory?.Id);
        }

        if (byGroup is TransactionRowViewModel transactionGroupViewModel)
        {
            var transaction = _repository.GetTransaction(transactionGroupViewModel.Id);

            transactionViewModel.Category = categories.FirstOrDefault(c => c.Id == transaction?.CategoryId);
            transactionViewModel.SubCategory = transactionViewModel.SubCategories.FirstOrDefault(s => s.Id == transaction?.SubCategoryId);
        }

        if (transactionViewModel.Category == null && categories.Count == 1)
        {
            transactionViewModel.Category = categories.FirstOrDefault();
        }

        if (transactionViewModel.SubCategory == null && subCategories.Count(s => s.CategoryId == transactionViewModel.Category?.Id) == 1)
        {
            transactionViewModel.SubCategory = subCategories.FirstOrDefault(s => s.CategoryId == transactionViewModel.Category?.Id);
        }

        transactionViewModel.SubCategoryText = transactionViewModel.SubCategory?.Name;

        if (transactionViewModel.SubCategory != null)
        {
            transactionViewModel.Sum = transactionViewModel.SubCategory.LastSum;
            transactionViewModel.ToSum = transactionViewModel.SubCategory.LastSum;
        }

        if (state.PeriodFrom == state.PeriodTo)
        {
            transactionViewModel.Date = state.PeriodFrom.Date;
        }
        else
        {
            transactionViewModel.Date = _lastTransactionDate;
        }

        transactionViewModel.RefreshSuggestedTags();

        return transactionViewModel;
    }

    private async Task EditTransaction(TransactionRowViewModel row)
    {
        if ((SelectedTransactionGroup ?? row) is not TransactionRowViewModel transactionGroupViewModel)
        {
            return;
        }

        // Сбор данных для формы — тяжёлая работа с базой, выполняем вне UI-потока.
        var (transaction, transactionViewModel) = await Task.Run<(Transaction?, BaseTransactionViewModel?)>(() =>
        {
            var entity = _repository.GetTransaction(transactionGroupViewModel.Id);
            if (entity == null)
            {
                return (null, null);
            }

            var flatAccounts = _stateManager.GetMainState().FlatAccounts;

            BaseTransactionViewModel viewModel;
            switch (entity)
            {
                case DebetTransaction:
                    viewModel = new DebetTransactionViewModel
                    {
                        Categories = GetCategories<DebetCategory, DebetCategoryViewModel>(),
                        SubCategories = GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel, DebetCategoryViewModel>(),
                    };
                    break;
                case CreditTransaction:
                    viewModel = new CreditTransactionViewModel
                    {
                        Categories = GetCategories<CreditCategory, CreditCategoryViewModel>(),
                        SubCategories = GetSubCategories<CreditSubCategory, CreditSubCategoryViewModel, CreditCategoryViewModel>(),
                    };
                    break;
                case TransferTransaction transfer:
                    viewModel = new TransferTransactionViewModel
                    {
                        Categories = GetCategories<TransferCategory, TransferCategoryViewModel>(),
                        SubCategories = GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel, TransferCategoryViewModel>(),
                        ToAccount = flatAccounts?.FirstOrDefault(a => a.Id == transfer.ToAccountId),
                        ToSum = transfer.ToSum,
                        IsTransfer = true,
                    };
                    break;
                default:
                    throw new ApplicationException("Unknown type of transaction " + entity);
            }

            viewModel.FillFrom(entity, _repository);
            viewModel.FlatAccounts = flatAccounts?.Where(c => !c.IsHidden).ToList();
            viewModel.Account = flatAccounts?.FirstOrDefault(a => a.Id == entity.AccountId);
            viewModel.Category = viewModel.Categories.FirstOrDefault(c => c.Id == entity.CategoryId);
            viewModel.SubCategory = viewModel.SubCategories.FirstOrDefault(c => c.Id == entity.SubCategoryId);
            viewModel.SubCategoryText = viewModel.SubCategory?.Name;
            viewModel.Comments = viewModel.SubCategory?.Comments ?? [];
            viewModel.RefreshSuggestedTags();

            viewModel.Categories = viewModel.Categories.Where(c => !c.IsHidden).ToList();
            viewModel.SubCategories = viewModel.SubCategories.Where(c => c.Category?.IsHidden == false).ToList();

            return (entity, viewModel);
        });

        if (transaction == null || transactionViewModel == null)
        {
            return;
        }

        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<BaseTransactionViewModel>(transactionViewModel));
        if (result != null)
        {
            await SaveTransaction(transaction, result);
        }
    }

    private async Task SaveTransaction(Transaction transaction, BaseTransactionViewModel transactionViewModel)
    {
        transaction.AccountId = transactionViewModel.Account?.Id;
        transaction.CategoryId = transactionViewModel.Category?.Id;
        transaction.Sum = transactionViewModel.Sum;
        transaction.Comment = transactionViewModel.Comment;
        transaction.Tags = transactionViewModel.GetTagsForSave();
        transaction.Date = transactionViewModel.Date!.Value.Date;
        transaction.LastChange = DateTime.Now;

        _lastTransactionDate = transaction.Date;

        Func<SubCategory> subCategoryFactory;
        switch (transaction)
        {
            case DebetTransaction:
                subCategoryFactory = () => new DebetSubCategory
                {
                    Id = Guid.NewGuid(),
                    Name = transactionViewModel.SubCategoryText!,
                    CategoryId = transaction.CategoryId
                };
                break;
            case CreditTransaction:
                subCategoryFactory = () => new CreditSubCategory
                {
                    Id = Guid.NewGuid(),
                    Name = transactionViewModel.SubCategoryText!,
                    CategoryId = transaction.CategoryId
                };
                break;
            case TransferTransaction transferTransaction:
                subCategoryFactory = () => new TransferSubCategory
                {
                    Id = Guid.NewGuid(),
                    Name = transactionViewModel.SubCategoryText!,
                    CategoryId = transaction.CategoryId
                };

                transferTransaction.ToAccountId = transactionViewModel.ToAccount?.Id;
                transferTransaction.ToSum = transactionViewModel.ToSum;
                break;
            default:
                throw new ApplicationException("Unknown type of transaction " + transaction);
        }

        transaction.SubCategoryId = null;
        if (!string.IsNullOrEmpty(transactionViewModel.SubCategoryText))
        {
            transaction.SubCategoryId = _repository.GetOrCreateSubCategory(transaction.CategoryId, transactionViewModel.SubCategoryText!, subCategoryFactory).Id;
        }

        var before = _repository.GetTransaction(transaction.Id);
        _repository.UpdateTransaction(transaction);
        await SendTransactionChanged(before, transaction);
        await RefreshTransactions(_stateManager.GetMainState());
    }

    [RelayCommand]
    public async Task ApplyFilterAsync()
    {
        _appliedCommentParts = (FilterCommentText ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _appliedTags.Clear();
        foreach (var tag in FilterTags.Where(t => t.IsChecked))
        {
            _appliedTags.Add(tag.Name);
        }

        IsFilterActive = _appliedCommentParts.Length > 0 || _appliedTags.Count > 0;
        await RefreshTransactions(_stateManager.GetMainState());
    }

    [RelayCommand]
    public async Task ResetFilterAsync()
    {
        FilterCommentText = string.Empty;
        _appliedCommentParts = [];
        _appliedTags.Clear();
        foreach (var tag in FilterTags)
        {
            tag.IsChecked = false;
        }

        IsFilterActive = false;
        await RefreshTransactions(_stateManager.GetMainState());
    }

    private bool MatchesFilter(Transaction transaction)
    {
        if (_appliedCommentParts.Length > 0)
        {
            var comment = transaction.Comment ?? string.Empty;
            foreach (var part in _appliedCommentParts)
            {
                if (!comment.Contains(part, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        if (_appliedTags.Count > 0)
        {
            if (transaction.Tags == null || !transaction.Tags.Any(t => _appliedTags.Contains(t.Trim())))
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateAvailableFilterTags(IReadOnlyCollection<Transaction> transactions)
    {
        var tags = transactions
            .SelectMany(t => t.Tags ?? [])
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Union(_appliedTags, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var checkedTags = FilterTags
            .Where(t => t.IsChecked)
            .Select(t => t.Name)
            .Union(_appliedTags, StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        FilterTags.Clear();
        foreach (var tag in tags)
        {
            FilterTags.Add(new FilterTagViewModel { Name = tag, IsChecked = checkedTags.Contains(tag) });
        }
    }

    [RelayCommand]
    public async Task ClearSelectionAsync()
    {
        if (SelectedTransactionGroup != null)
        {
            SelectedTransactionGroup.IsSelected = false;
            SelectedTransactionGroup = null;
        }

        foreach (var item in SelectedTransactionGroups)
        {
            item.IsSelected = false;
        }
        SelectedTransactionGroups.Clear();
    }

    private async Task RefreshTransactions(MainState state)
    {
        var selected = SelectedTransactionGroup;
        await ClearSelectionAsync();

        var allTransactions = _repository.GetTransactions(new TransactionsFilter
        {
            AccountId = state.SelectedAccountId,
            PeriodFrom = state.PeriodFrom,
            PeriodTo = state.PeriodTo
        }).ToList();

        UpdateAvailableFilterTags(allTransactions);

        var transactions = IsFilterActive
            ? allTransactions.Where(MatchesFilter)
            : allTransactions;

        var debetTransactionsTemp = new SummaryTransactionsGroup { IsDebet = true };
        var creditTransactionsTemp = new SummaryTransactionsGroup { IsDebet = false };
        var transferTransactionsTemp = new SummaryTransactionsGroup { };

        var transactionsDyDates = new List<TransactionsByDatesGroup>();

        var lastDate = DateTime.MinValue;
        TransactionsByDatesGroup? lastDateGroup = null;

        foreach (var transaction in transactions)
        {
            if (transaction.Date != lastDate || lastDateGroup == null)
            {
                lastDateGroup?.SortByLastChange();
                lastDateGroup = new TransactionsByDatesGroup { Date = transaction.Date, Parent = this };
                lastDate = transaction.Date;
                transactionsDyDates.Add(lastDateGroup);
            }

            TransactionRowViewModel? transactionView = null;
            if (transaction is DebetTransaction)
            {
                transactionView = FillTransactions<DebetCategoryViewModel, DebetSubCategoryViewModel>(debetTransactionsTemp, 
                    transaction, 
                    selected, 
                    (_, _) => true);
                lastDateGroup.Sum += transaction.Sum;
            }
            else if (transaction is CreditTransaction)
            {
                transactionView = FillTransactions<CreditCategoryViewModel, CreditSubCategoryViewModel>(creditTransactionsTemp, 
                    transaction, 
                    selected, 
                    (_, _) => false);
                lastDateGroup.Sum -= transaction.Sum;
            }
            else if (transaction is TransferTransaction transferTransaction)
            {
                transactionView = FillTransactions<TransferCategoryViewModel, TransferSubCategoryViewModel>(transferTransactionsTemp, 
                    transaction,
                    selected, 
                    (t, g) => g is TransactionRowViewModel && transaction is TransferTransaction transfer && transfer.ToAccountId == state.SelectedAccountId
                );
                lastDateGroup.Sum += state.SelectedAccountId.HasValue ? 
                    (state.SelectedAccountId == transferTransaction.AccountId ? -transferTransaction.Sum : transferTransaction.ToSum) 
                    : 0;
            }

            if (transactionView != null && lastDateGroup != null)
            {
                lastDateGroup.Transactions.Add(transactionView);
            }
        }
        lastDateGroup?.SortByLastChange();

        debetTransactionsTemp.CalcPercentsAndSort();
        creditTransactionsTemp.CalcPercentsAndSort();
        transferTransactionsTemp.CalcPercentsAndSort();

        Total = debetTransactionsTemp.Sum - creditTransactionsTemp.Sum;
        DebetTransactions = debetTransactionsTemp;
        CreditTransactions = creditTransactionsTemp;
        TransferTransactions = transferTransactionsTemp;

        TransactionsDyDates.Clear();
        TransactionsDyDates.AddRange(transactionsDyDates.Take(_configuration.Get().Transactions.MaxTransactionsByDate));
    }

    private TransactionRowViewModel FillTransactions<C, S>(SummaryTransactionsGroup summary, 
            Transaction transaction,
            BaseTransactionsGroupViewModel? selected, 
            Func<Transaction, BaseTransactionsGroupViewModel?, bool> isDebet)
        where C : BaseCategoryViewModel, new() where S : BaseSubCategoryViewModel, new() 
    {
        summary.Sum += transaction.Sum;
        var category = summary.TryAddCategory(transaction.CategoryId.GetValueOrDefault(), () => 
        {
            return transaction.CategoryId == null ? new CategoryTransactionsGroupViewModel { IsDebet = isDebet(transaction, null), Parent = this }
             : new CategoryTransactionsGroupViewModel
             {
                 Category = GetCategory<C>(transaction.CategoryId.Value),
                 Parent = this,
                 IsExpanded = _openedNodes.Contains(transaction.CategoryId.Value),
                 IsSelected = selected is CategoryTransactionsGroupViewModel selectedCategory && selectedCategory!.Category?.Id == transaction.CategoryId,
                 IsDebet = isDebet(transaction, null),
             };
        });
        SelectedTransactionGroup = category.IsSelected ? category : SelectedTransactionGroup;
        category.Sum += transaction.Sum;

        var subCategory = category.TryAddSubCategory(transaction.SubCategoryId.GetValueOrDefault(), () =>
        {
            return transaction.SubCategoryId == null ? new SubCategoryTransactionsGroupViewModel { IsDebet = isDebet(transaction, null), Parent = this }
            : new SubCategoryTransactionsGroupViewModel
            {
                Parent = this,
                SubCategory = GetSubCategory<S>(transaction.SubCategoryId.Value),
                IsExpanded = _openedNodes.Contains(transaction.SubCategoryId.Value),
                IsSelected = selected is SubCategoryTransactionsGroupViewModel selectedSubCategory
                                    && selectedSubCategory!.SubCategory?.Id == transaction.SubCategoryId,
                IsDebet = isDebet(transaction, null),
            };
        });
        if (transaction.SubCategoryId == null)
        {
            subCategory = category.SubCategories.FirstOrDefault(t => t.SubCategory == null);
            if (subCategory == null)
            {
                subCategory = new SubCategoryTransactionsGroupViewModel { IsDebet = isDebet(transaction, null), Parent = this };
                category.SubCategories.Add(subCategory);
            }
        }
        SelectedTransactionGroup = subCategory.IsSelected ? subCategory : SelectedTransactionGroup;
        subCategory.Sum += transaction.Sum;

        var viewTransaction = new TransactionRowViewModel
        {
            Id = transaction.Id,
            Date = transaction.Date,
            LastChange = transaction.LastChange,
            Comment = transaction.Comment,
            Tags = transaction.Tags?
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .ToArray() ?? [],
            Sum = transaction.Sum,
            Category = category.Category,
            SubCategory = subCategory.SubCategory,
            Parent = this,
        };
        viewTransaction.IsDebet = isDebet(transaction, viewTransaction);

        viewTransaction.IsSelected = selected is TransactionRowViewModel selectedTransaction && selectedTransaction.Id == transaction.Id;
        SelectedTransactionGroup = viewTransaction.IsSelected ? viewTransaction : SelectedTransactionGroup;

        subCategory.Transactions.Add(viewTransaction);

        return viewTransaction;
    }

    private BaseCategoryViewModel GetCategory<C>(Guid id) where C: BaseCategoryViewModel, new()
    {
        lock (_listsCacheLock)
        {
            if (_categoriesCache.TryGetValue(id, out var cached))
            {
                return cached;
            }
        }

        var category = new C();
        category.FillFrom(id, _repository);

        lock (_listsCacheLock)
        {
            _categoriesCache[id] = category;
        }

        return category;
    }

    private BaseSubCategoryViewModel GetSubCategory<C>(Guid id) where C : BaseSubCategoryViewModel, new()
    {
        lock (_listsCacheLock)
        {
            if (_subCategoriesCache.TryGetValue(id, out var cached))
            {
                return cached;
            }
        }

        var subCategory = new C();
        subCategory.FillFrom(id, _repository);

        lock (_listsCacheLock)
        {
            _subCategoriesCache[id] = subCategory;
        }

        return subCategory;
    }

    private List<BaseCategoryViewModel> GetCategories<T, N>() where T : Category where N : BaseCategoryViewModel, new()
    {
        lock (_listsCacheLock)
        {
            if (_categoryListsCache.TryGetValue(typeof(T), out var cached))
            {
                return cached;
            }
        }

        var categories = _repository.GetCategories().OfType<T>().OrderBy(c => c.Name).Select(c =>
        {
            var category = new N();
            category.FillFrom(c, _repository);
            return category;
        });

        var result = categories.Select(c => (BaseCategoryViewModel)c).ToList();

        lock (_listsCacheLock)
        {
            _categoryListsCache[typeof(T)] = result;
        }

        return result;
    }

    private List<BaseSubCategoryViewModel> GetSubCategories<T, N, C>()
        where T : SubCategory
        where N : BaseSubCategoryViewModel, new()
        where C: BaseCategoryViewModel, new()
    {
        lock (_listsCacheLock)
        {
            if (_subCategoryListsCache.TryGetValue(typeof(T), out var cached))
            {
                return cached;
            }
        }

        var typedSubCategories = _repository.GetSubCategories().OfType<T>().ToList();
        var from = DateTime.Today.AddYears(-1);
        var subCategoriesBySums = _repository.GetLastSumsBySubCategories(from, typedSubCategories.Select(c => c.Id))
            .ToDictionary(s => s.SubCategoryId);
        var subCategoriesByComments = _repository.GetCommentsBySubCategories(from)
            .ToDictionary(s => s.SubCategoryId);
        var subCategoriesByTags = _repository.GetTagsBySubCategories(from)
            .ToDictionary(s => (s.SubCategoryId, s.CategoryId));

        var subCategories = typedSubCategories.OrderBy(c => c.Name).Select(c => new N
        {
            Id = c.Id,
            Name = c.Name,
            CategoryId = c.CategoryId,
            Category = GetCategory<C>(c.CategoryId.GetValueOrDefault()),
            LastSum = subCategoriesBySums.TryGetValue(c.Id, out var sum) ? sum.Sum : 0m,
            Comments = subCategoriesByComments.TryGetValue(c.Id, out var comments) ? comments.Comments : [],
            Tags = subCategoriesByTags.TryGetValue((c.Id, c.CategoryId.GetValueOrDefault()), out var tags) ? tags.Tags : [],
        });

        var result = subCategories.Select(c => (BaseSubCategoryViewModel)c).ToList();

        lock (_listsCacheLock)
        {
            _subCategoryListsCache[typeof(T)] = result;
        }

        return result;
    }

    private void InvalidateListsCache(bool categories, bool subCategories)
    {
        lock (_listsCacheLock)
        {
            if (categories)
            {
                _categoryListsCache.Clear();
            }

            if (subCategories)
            {
                _subCategoryListsCache.Clear();
            }
        }
    }
}
