using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using DynamicData;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.State;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class TransactionsViewModel : ViewModelBase
{
    private readonly IRepository _repository;
    private readonly IStateManager _stateManager;
    private readonly IGlobalConfiguration _configuration;
    private decimal _total = 0m;

    private DateTime _lastTransactionDate = DateTime.Today;

    private readonly HashSet<Guid> _openedNodes = [];

    private readonly Dictionary<Guid, BaseCategoryViewModel> _categoriesCache = [];
    private readonly Dictionary<Guid, BaseSubCategoryViewModel> _subCategoriesCache = [];

    private SummaryTransactionsGroup _debetTransactions = new();
    private SummaryTransactionsGroup _creditTransactions = new();
    private SummaryTransactionsGroup _transferTransactions = new();

    private ObservableCollection<TransactionsByDatesGroup> _transactionsDyDates = [];

    private BaseTransactionsGroupViewModel? _selectedTransactionGroup = null;

    public SummaryTransactionsGroup DebetTransactions
    {
        get => _debetTransactions;
        set => this.RaiseAndSetIfChanged(ref _debetTransactions, value);
    }

    public SummaryTransactionsGroup CreditTransactions
    {
        get => _creditTransactions;
        set => this.RaiseAndSetIfChanged(ref _creditTransactions, value);
    }

    public SummaryTransactionsGroup TransferTransactions
    {
        get => _transferTransactions;
        set => this.RaiseAndSetIfChanged(ref _transferTransactions, value);
    }

    public ObservableCollection<TransactionsByDatesGroup> TransactionsDyDates
    {
        get => _transactionsDyDates;
        set => this.RaiseAndSetIfChanged(ref _transactionsDyDates, value);
    }

    public ICommand AddDebetCommand { get; }

    public ICommand AddCreditCommand { get; }

    public ICommand AddTransferCommand { get; }

    public ICommand ClearSelectionCommand { get; }

    public ICommand ToggleExpand { get; }

    public ICommand SelectCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand CopyCommand { get; }

    public ICommand DeleteCommand { get; }

    public decimal Total
    {
        get => _total;
        set => this.RaiseAndSetIfChanged(ref _total, value);
    }

    public BaseTransactionsGroupViewModel? SelectedTransactionGroup
    {
        get => _selectedTransactionGroup;
        set => this.RaiseAndSetIfChanged(ref _selectedTransactionGroup, value);
    }

    public TransactionsViewModel(IRepository repository, IStateManager stateManager, IGlobalConfiguration configuration)
    {
        _repository = repository;
        _stateManager = stateManager;
        _configuration = configuration;

        SubscribeMessages();

        AddDebetCommand = ReactiveCommand.CreateFromTask(AddDebetTransaction);
        AddCreditCommand = ReactiveCommand.CreateFromTask(AddCreditTransaction);
        AddTransferCommand = ReactiveCommand.CreateFromTask(AddTransferTransaction);

        ClearSelectionCommand = ReactiveCommand.Create(ClearSelection);
        
        ToggleExpand = ReactiveCommand.Create((BaseTransactionsGroupViewModel child) =>
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
        });

        SelectCommand = ReactiveCommand.Create((BaseTransactionsGroupViewModel child) =>
        {
            ClearSelection();
            SelectedTransactionGroup = child;
            SelectedTransactionGroup.IsSelected = true;
        });

        EditCommand = ReactiveCommand.CreateFromTask(async (TransactionRowViewModel child) =>
        {
            await EditTransaction();
        });

        CopyCommand = ReactiveCommand.Create((TransactionRowViewModel child) =>
        {
            RxApp.MainThreadScheduler.Schedule(async () =>
            {
                await CopyTransaction(child);
            });
        });

        DeleteCommand = ReactiveCommand.Create((TransactionRowViewModel child) =>
        {
            DeleteTransaction(child);
        });
    }

    private void SubscribeMessages()
    {
        MessageBus.Current.Listen<MainStateChangedMessage>()
            .Where(m => m.State != null)
            .Subscribe(m => RefreshTransactions(m.State));

        MessageBus.Current.Listen<CategoryUpdateMessage>()
            .Where(m => m.CategoryId != null)
            .Subscribe(m =>
            {
                _categoriesCache.Remove(m.CategoryId.GetValueOrDefault());
            });
    }

    private async Task AddTransferTransaction()
    {
        var transactionViewModel = CreateNewTransaction<TransferTransactionViewModel>(
            GetCategories<TransferCategory, TransferCategoryViewModel>(),
            GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel, TransferCategoryViewModel>(),
            null
            );
        transactionViewModel.IsTransfer = true;

        var result = await BaseTransactionViewModel.ShowDialog.Handle(transactionViewModel);
        if (result != null)
        {
            SaveTransaction(new TransferTransaction { Id = Guid.NewGuid() }, result);
        }
    }

    private async Task AddCreditTransaction()
    {
        var transactionViewModel = CreateNewTransaction<CreditTransactionViewModel>(
            GetCategories<CreditCategory, CreditCategoryViewModel>(),
            GetSubCategories<CreditSubCategory, CreditSubCategoryViewModel, CreditCategoryViewModel>(),
            null
            );

        var result = await BaseTransactionViewModel.ShowDialog.Handle(transactionViewModel);
        if (result != null)
        {
            SaveTransaction(new CreditTransaction { Id = Guid.NewGuid() }, result);
        }
    }

    private async Task AddDebetTransaction()
    {
        var transactionViewModel = CreateNewTransaction<DebetTransactionViewModel>(
            GetCategories<DebetCategory, DebetCategoryViewModel>(),
            GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel, DebetCategoryViewModel>(),
            null
            );

        var result = await BaseTransactionViewModel.ShowDialog.Handle(transactionViewModel);
        if (result != null)
        {
            SaveTransaction(new DebetTransaction { Id = Guid.NewGuid() }, result);
        }
    }

    private void DeleteTransaction(TransactionRowViewModel transactionGroupViewModel)
    {
        var transaction = _repository.GetTransaction(transactionGroupViewModel.Id);
        _repository.DeleteTransaction(transactionGroupViewModel.Id);
        SendTransactionChanged(transaction, null);
        RefreshTransactions(_stateManager.GetMainState());
    }

    private async Task CopyTransaction(TransactionRowViewModel transactionGroupViewModel)
    {
        var transaction = _repository.GetTransaction(transactionGroupViewModel.Id);

        BaseTransactionViewModel? transactionViewModel;
        if (transaction is DebetTransaction)
        {
            transactionViewModel = CreateNewTransaction<DebetTransactionViewModel>(
                GetCategories<DebetCategory, DebetCategoryViewModel>(),
                GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel, DebetCategoryViewModel>(),
                transactionGroupViewModel
                );
        }
        else if (transaction is CreditTransaction)
        {
            transactionViewModel = CreateNewTransaction<CreditTransactionViewModel>(
                GetCategories<CreditCategory, CreditCategoryViewModel>(),
                GetSubCategories<CreditSubCategory, CreditSubCategoryViewModel, CreditCategoryViewModel>(),
                transactionGroupViewModel
                );
        }
        else if (transaction is TransferTransaction)
        {
            transactionViewModel = CreateNewTransaction<TransferTransactionViewModel>(
                GetCategories<TransferCategory, TransferCategoryViewModel>(),
                GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel, TransferCategoryViewModel>(),
                transactionGroupViewModel
                );
            transactionViewModel.IsTransfer = true;
        }
        else
        {
            return;
        }

        transactionViewModel.Sum = transaction.Sum;
        transactionViewModel.Comment = transaction.Comment;

        var result = await BaseTransactionViewModel.ShowDialog.Handle(transactionViewModel);
        if (result != null)
        {
            transaction.Id = Guid.NewGuid();
            SaveTransaction(transaction, result);
        }
    }

    private static void SendTransactionChanged(Transaction? before, Transaction? after)
    {
        MessageBus.Current.SendMessage(new TransactionChangedMessage(before, after));
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

        return transactionViewModel;
    }

    private async Task EditTransaction()
    {
        if (SelectedTransactionGroup is not TransactionRowViewModel transactionGroupViewModel)
        {
            return;
        }

        var transaction = _repository.GetTransaction(transactionGroupViewModel.Id);
        if (transaction == null)
        {
            return;
        }

        var flatAccounts = _stateManager.GetMainState().FlatAccounts;

        BaseTransactionViewModel transactionViewModel;
        switch(transaction)
        {
            case DebetTransaction:
                transactionViewModel = new DebetTransactionViewModel
                {
                    Categories = GetCategories<DebetCategory, DebetCategoryViewModel>(),
                    SubCategories = GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel, DebetCategoryViewModel>(),
                };
                break;
            case CreditTransaction:
                transactionViewModel = new CreditTransactionViewModel
                {
                    Categories = GetCategories<CreditCategory, CreditCategoryViewModel>(),
                    SubCategories = GetSubCategories<CreditSubCategory, CreditSubCategoryViewModel, CreditCategoryViewModel>(),
                };
                break;
            case TransferTransaction transfer:
                transactionViewModel = new TransferTransactionViewModel
                {
                    Categories = GetCategories<TransferCategory, TransferCategoryViewModel>(),
                    SubCategories = GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel, TransferCategoryViewModel>(),
                    ToAccount = flatAccounts?.FirstOrDefault(a => a.Id == transfer.ToAccountId),
                    ToSum = transfer.ToSum,
                    IsTransfer = true,
                };
                break;
            default:
                throw new ApplicationException("Unknown type of transaction " + transaction);
        }

        transactionViewModel.FillFrom(transaction, _repository);
        transactionViewModel.FlatAccounts = flatAccounts?.Where(c => !c.IsHidden).ToList();
        transactionViewModel.Account = flatAccounts?.FirstOrDefault(a => a.Id == transaction.AccountId);
        transactionViewModel.Category = transactionViewModel.Categories.FirstOrDefault(c => c.Id == transaction.CategoryId);
        transactionViewModel.SubCategory = transactionViewModel.SubCategories.FirstOrDefault(c => c.Id == transaction.SubCategoryId);
        transactionViewModel.SubCategoryText = transactionViewModel.SubCategories.FirstOrDefault(c => c.Id == transaction.SubCategoryId)?.Name;

        transactionViewModel.Categories = transactionViewModel.Categories.Where(c => !c.IsHidden).ToList();
        transactionViewModel.SubCategories = transactionViewModel.SubCategories.Where(c => c.Category?.IsHidden == false).ToList();

        if (transactionViewModel == null)
        {
            return;
        }

        var result = await BaseTransactionViewModel.ShowDialog.Handle(transactionViewModel);
        if (result != null)
        {
            SaveTransaction(transaction, result);
        }
    }

    private void SaveTransaction(Transaction transaction, BaseTransactionViewModel transactionViewModel)
    {
        transaction.AccountId = transactionViewModel.Account?.Id;
        transaction.CategoryId = transactionViewModel.Category?.Id;
        transaction.Sum = transactionViewModel.Sum;
        transaction.Comment = transactionViewModel.Comment;
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
        SendTransactionChanged(before, transaction);
        RefreshTransactions(_stateManager.GetMainState());
    }

    private void ClearSelection()
    {
        if (SelectedTransactionGroup != null)
        {
            SelectedTransactionGroup.IsSelected = false;
            SelectedTransactionGroup = null;
        }
    }

    private void RefreshTransactions(MainState state)
    {
        var selected = SelectedTransactionGroup;
        ClearSelection();

        var transactions = _repository.GetTransactions(new TransactionsFilter
        {
            AccountId = state.SelectedAccountId,
            PeriodFrom = state.PeriodFrom,
            PeriodTo = state.PeriodTo
        });

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
        if (!_categoriesCache.TryGetValue(id, out var category))
        {
            category = new C();
            category.FillFrom(id, _repository);

            _categoriesCache.Add(id, category);
        }

        return category;
    }

    private BaseSubCategoryViewModel GetSubCategory<C>(Guid id) where C : BaseSubCategoryViewModel, new()
    {
        if (!_subCategoriesCache.TryGetValue(id, out var subCategory))
        {
            subCategory = new C();
            subCategory.FillFrom(id, _repository);

            _subCategoriesCache.Add(id, subCategory);
        }

        return subCategory;
    }

    private List<BaseCategoryViewModel> GetCategories<T, N>() where T : Category where N : BaseCategoryViewModel, new()
    {
        var categories = _repository.GetCategories().OfType<T>().OrderBy(c => c.Name).Select(c =>
        {
            var category = new N();
            category.FillFrom(c, _repository);
            return category;
        });

        return categories.Select(c => (BaseCategoryViewModel)c).ToList();
    }

    private List<BaseSubCategoryViewModel> GetSubCategories<T, N, C>() 
        where T : SubCategory 
        where N : BaseSubCategoryViewModel, new()
        where C: BaseCategoryViewModel, new()
    {
        var typedSubCategories = _repository.GetSubCategories().OfType<T>();
        var subCategoriesBySums = _repository.GetLastSumsBySubCategories(DateTime.Today.AddYears(-1), typedSubCategories.Select(c => c.Id));
        var subCategoriesByComments = _repository.GetCommentsBySubCategories(DateTime.Today.AddYears(-1));

        var subCategories = typedSubCategories.OrderBy(c => c.Name).Select(c => new N
        {
            Id = c.Id,
            Name = c.Name,
            CategoryId = c.CategoryId,
            Category = GetCategory<C>(c.CategoryId.GetValueOrDefault()),
            LastSum = subCategoriesBySums.FirstOrDefault(s => s.SubCategoryId == c.Id)?.Sum ?? 0m,
            Comments = subCategoriesByComments.FirstOrDefault(s => s.SubCategoryId == c.Id)?.Comments ?? [],
        });

        return subCategories.Select(c => (BaseSubCategoryViewModel)c).ToList();
    }
}
