using Avalonia.Styling;
using DynamicData;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.State;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class TransactionsViewModel : ViewModelBase
{
    private readonly IRepository _repository;
    private readonly IStateManager _stateManager;
    private decimal _total = 0m;

    private DateTime _lastTransactionDate = DateTime.Today;

    private HashSet<Guid> _openedNodes = new();

    private readonly Dictionary<Guid, BaseCategoryViewModel> _categoriesCache = new();
    private readonly Dictionary<Guid, BaseSubCategoryViewModel> _subCategoriesCache = new();

    private TransactionsGroup _debetTransactions = new();
    private TransactionsGroup _creditTransactions = new();
    private TransactionsGroup _transferTransactions = new();

    private ObservableCollection<TransactionsByDatesGroup> _transactionsDyDates = new();

    private BaseTransactionsGroupViewModel? _selectedTransactionGroup = null;

    public TransactionsGroup DebetTransactions
    {
        get => _debetTransactions;
        set => this.RaiseAndSetIfChanged(ref _debetTransactions, value);
    }

    public TransactionsGroup CreditTransactions
    {
        get => _creditTransactions;
        set => this.RaiseAndSetIfChanged(ref _creditTransactions, value);
    }

    public TransactionsGroup TransferTransactions
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

    public ICommand EditCommand { get; }

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

    public TransactionsViewModel(IRepository repository, IStateManager stateManager)
    {
        _repository = repository;
        _stateManager = stateManager;

        SubscribeMessages();

        AddDebetCommand = ReactiveCommand.CreateFromTask(AddDebetTransaction);
        AddCreditCommand = ReactiveCommand.CreateFromTask(AddCreditTransaction);
        AddTransferCommand = ReactiveCommand.CreateFromTask(AddTransferTransaction);
        EditCommand = ReactiveCommand.CreateFromTask(EditTransaction);
    }

    private void SubscribeMessages()
    {
        MessageBus.Current.Listen<MainStateChangedMessage>()
            .Where(m => m.State != null)
            .Subscribe(m => RefreshTransactions(m.State));

        MessageBus.Current.Listen<TransactionGroupSelectMessage>()
            .Where(m => m.Element != null)
            .Subscribe(m =>
            {
                ClearSelection();
                SelectedTransactionGroup = m.Element;
                SelectedTransactionGroup.IsSelected = true;
            });

        MessageBus.Current.Listen<TransactionGroupExpandMessage>()
            .Where(m => m.Element != null)
            .Subscribe(m =>
            {
                var id = ((m.Element as CategoryTransactionsGroupViewModel)?.Category?.Id
                        ?? (m.Element as SubCategoryTransactionsGroupViewModel)?.SubCategory?.Id)
                        ?? Guid.Empty;
                if (m.Element.IsExpanded)
                {
                    _openedNodes.Add(id);
                }
                else
                {
                    _openedNodes.Remove(id);
                }
            });

        MessageBus.Current.Listen<TransactionGroupEditMessage>()
            .Where(m => m.Element != null)
            .Subscribe(m =>
            {
                if (EditCommand!.CanExecute(null))
                {
                    EditCommand.Execute(null);
                }
            });

        MessageBus.Current.Listen<TransactionGroupDeleteMessage>()
            .Where(m => m.Element != null)
            .Subscribe(m =>
            {
                DeleteTransaction(m.Element);
            });

        MessageBus.Current.Listen<TransactionGroupCopyMessage>()
            .Where(m => m.Element != null)
            .Subscribe(async m =>
            {
                await CopyTransaction(m.Element);
            });

        MessageBus.Current.Listen<CategoryUpdateMessage>()
            .Where(m => m.CategoryId != null)
            .Subscribe(m =>
            {
                if (_categoriesCache.ContainsKey(m.CategoryId.GetValueOrDefault()))
                {
                    _categoriesCache.Remove(m.CategoryId.GetValueOrDefault());
                }
            });
    }

    private async Task AddTransferTransaction()
    {
        var transactionViewModel = CreateNewTransaction<TransferTransactionViewModel>(
            GetCategories<TransferCategory, TransferCategoryViewModel>(),
            GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel, TransferCategoryViewModel>(),
            SelectedTransactionGroup
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
            SelectedTransactionGroup
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
            SelectedTransactionGroup
            );

        var result = await BaseTransactionViewModel.ShowDialog.Handle(transactionViewModel);
        if (result != null)
        {
            SaveTransaction(new DebetTransaction { Id = Guid.NewGuid() }, result);
        }
    }

    private void DeleteTransaction(TransactionGroupViewModel transactionGroupViewModel)
    {
        var transaction = _repository.GetTransaction(transactionGroupViewModel.Id);
        _repository.DeleteTransaction(transactionGroupViewModel.Id);
        SendTransactionChanged(transaction, null);
        RefreshTransactions(_stateManager.GetMainState());
    }

    private async Task CopyTransaction(TransactionGroupViewModel transactionGroupViewModel)
    {
        var transaction = _repository.GetTransaction(transactionGroupViewModel.Id);

        BaseTransactionViewModel? transactionViewModel = null;

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

    private void SendTransactionChanged(Transaction? before, Transaction? after)
    {
        MessageBus.Current.SendMessage(new TransactionChangedMessage
        {
            Before = before,
            After = after,
        });
    }

    private BaseTransactionViewModel CreateNewTransaction<T>(IList<BaseCategoryViewModel> categories, 
                                                            IList<BaseSubCategoryViewModel> subCategories,
                                                            BaseTransactionsGroupViewModel? byGroup) 
        where T : BaseTransactionViewModel, new()
    {
        var state = _stateManager.GetMainState();
        var flatAccounts = state.FlatAccounts;
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

        if (byGroup is TransactionGroupViewModel transactionGroupViewModel)
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
        if (SelectedTransactionGroup is not TransactionGroupViewModel transactionGroupViewModel)
        {
            return;
        }

        BaseTransactionViewModel? transactionViewModel = null;
        var transaction = _repository.GetTransaction(transactionGroupViewModel.Id);
        if (transaction == null)
        {
            return;
        }

        var flatAccounts = _stateManager.GetMainState().FlatAccounts;
        if (transaction is DebetTransaction)
        {
            transactionViewModel = new DebetTransactionViewModel
            {
                Categories = GetCategories<DebetCategory, DebetCategoryViewModel>(),
                SubCategories = GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel, DebetCategoryViewModel>(),
            };
        }
        else if (transaction is CreditTransaction)
        {
            transactionViewModel = new CreditTransactionViewModel
            {
                Categories = GetCategories<CreditCategory, CreditCategoryViewModel>(),
                SubCategories = GetSubCategories<CreditSubCategory, CreditSubCategoryViewModel, CreditCategoryViewModel>(),
            };
        }
        else if (transaction is TransferTransaction transfer)
        {
            transactionViewModel = new TransferTransactionViewModel
            {
                Categories = GetCategories<TransferCategory, TransferCategoryViewModel>(),
                SubCategories = GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel, TransferCategoryViewModel>(),
                ToAccount = flatAccounts?.FirstOrDefault(a => a.Id == transfer.ToAccountId),
                ToSum = transfer.ToSum,
                IsTransfer = true,
            };
        }
        else
        {
            return;
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

        if (!string.IsNullOrEmpty(transactionViewModel.SubCategoryText))
        {
            Func<SubCategory> factory = () => new DebetSubCategory
            {
                Id = Guid.NewGuid(),
                Name = transactionViewModel.SubCategoryText!,
                CategoryId = transaction.CategoryId
            };

            if (transaction is DebetTransaction)
            {
            }
            else if (transaction is CreditTransaction)
            {
                factory = () => new CreditSubCategory
                {
                    Id = Guid.NewGuid(),
                    Name = transactionViewModel.SubCategoryText!,
                    CategoryId = transaction.CategoryId
                };
            }
            else if (transaction is TransferTransaction transferTransaction)
            {
                factory = () => new TransferSubCategory
                {
                    Id = Guid.NewGuid(),
                    Name = transactionViewModel.SubCategoryText!,
                    CategoryId = transaction.CategoryId
                };

                transferTransaction.ToAccountId = transactionViewModel.ToAccount?.Id;
                transferTransaction.ToSum = transactionViewModel.ToSum;
            }

            transaction.SubCategoryId = _repository.GetOrCreateSubCategory(transaction.CategoryId, transactionViewModel.SubCategoryText!, factory).Id;
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

        Dictionary<Guid, CategoryTransactionsGroupViewModel> debetCategoryGroupsCache = [];
        Dictionary<Guid, SubCategoryTransactionsGroupViewModel> debetSubCategoryGroupsCache = [];
        Dictionary<Guid, CategoryTransactionsGroupViewModel> creditCategoryGroupsCache = [];
        Dictionary<Guid, SubCategoryTransactionsGroupViewModel> creditSubCategoryGroupsCache = [];
        Dictionary<Guid, CategoryTransactionsGroupViewModel> transferCategoryGroupsCache = [];
        Dictionary<Guid, SubCategoryTransactionsGroupViewModel> transferSubCategoryGroupsCache = [];

        var debetTransactions = new List<CategoryTransactionsGroupViewModel>();
        var creditTransactions = new List<CategoryTransactionsGroupViewModel>();
        var transferTransactions = new List<CategoryTransactionsGroupViewModel>();

        var transactionsDyDates = new ObservableCollection<TransactionsByDatesGroup>();

        var lastDate = DateTime.MinValue;
        TransactionsByDatesGroup? lastDateGroup = null;

        foreach (var transaction in transactions)
        {
            if (transaction.Date != lastDate)
            {
                if (lastDateGroup != null)
                {
                    var sortedTransactions = lastDateGroup.Transactions.OrderBy(t => t.LastChange).ToList();
                    lastDateGroup.Transactions.Clear();
                    lastDateGroup.Transactions.AddRange(sortedTransactions);
                    lastDateGroup.Sum = lastDateGroup.Transactions.Sum(t => t.SumForTotal);
                    lastDateGroup.IsDebet = lastDateGroup.Sum >= 0;
                }

                lastDateGroup = new TransactionsByDatesGroup { Date = transaction.Date };
                lastDate = transaction.Date;
                transactionsDyDates.Add(lastDateGroup);
            }

            TransactionGroupViewModel? transactionView = null;
            if (transaction is DebetTransaction)
            {
                transactionView = FillTransactions<DebetCategoryViewModel, DebetSubCategoryViewModel>(debetTransactions, 
                    transaction, 
                    debetCategoryGroupsCache, 
                    debetSubCategoryGroupsCache,
                    selected, 
                    (_, _) => true);
                transactionView.SumForTotal = transaction.Sum;
            }
            else if (transaction is CreditTransaction)
            {
                transactionView = FillTransactions<CreditCategoryViewModel, CreditSubCategoryViewModel>(creditTransactions, 
                    transaction, 
                    creditCategoryGroupsCache,
                    creditSubCategoryGroupsCache,
                    selected, 
                    (_, _) => false);
                transactionView.SumForTotal = -transaction.Sum;
            }
            else if (transaction is TransferTransaction transferTransaction)
            {
                transactionView = FillTransactions<TransferCategoryViewModel, TransferSubCategoryViewModel>(transferTransactions, 
                    transaction,
                    transferCategoryGroupsCache,
                    transferSubCategoryGroupsCache,
                    selected, 
                    (t, g) => g is TransactionGroupViewModel && transaction is TransferTransaction transfer && transfer.ToAccountId == state.SelectedAccountId
                );
                transactionView.SumForTotal = state.SelectedAccountId.HasValue ? 
                    (state.SelectedAccountId == transferTransaction.AccountId ? -transferTransaction.Sum : transferTransaction.ToSum) 
                    : 0;
            }

            if (transactionView != null && lastDateGroup != null)
            {
                lastDateGroup.Transactions.Add(transactionView);
            }
        }

        var debetTransactionsTemp = new TransactionsGroup { Sum = debetTransactions.Sum(t => t.Sum), IsDebet = true };
        CalcPercents(debetTransactionsTemp.Sum, debetTransactions);
        debetTransactionsTemp.Categories.AddRange(debetTransactions);

        var creditTransactionsTemp = new TransactionsGroup { Sum = creditTransactions.Sum(t => t.Sum), IsDebet = false };
        CalcPercents(creditTransactionsTemp.Sum, creditTransactions);
        creditTransactionsTemp.Categories.AddRange(creditTransactions);

        var transferTransactionsTemp = new TransactionsGroup { Sum = transferTransactions.Sum(t => t.Sum) };
        CalcPercents(transferTransactionsTemp.Sum, transferTransactions);
        transferTransactionsTemp.Categories.AddRange(transferTransactions);

        Total = debetTransactionsTemp.Sum - creditTransactionsTemp.Sum;
        DebetTransactions = debetTransactionsTemp;
        CreditTransactions = creditTransactionsTemp;
        TransferTransactions = transferTransactionsTemp;
        TransactionsDyDates = transactionsDyDates;
    }

    private void CalcPercents(decimal sum, IEnumerable<CategoryTransactionsGroupViewModel> categories)
    {
        foreach (var category in categories)
        {
            category.Percent = Math.Round(category.Sum / sum, 4, MidpointRounding.AwayFromZero);
            foreach (var subCategory in category.SubCategories)
            {
                subCategory.Percent = Math.Round(subCategory.Sum / sum, 4, MidpointRounding.AwayFromZero);
                foreach (var transaction in subCategory.Transactions)
                {
                    transaction.Percent = Math.Round(transaction.Sum / sum, 4, MidpointRounding.AwayFromZero);
                }
            }
        }
    }

    private TransactionGroupViewModel FillTransactions<C, S>(List<CategoryTransactionsGroupViewModel> transactions, 
            Transaction transaction,
            Dictionary<Guid, CategoryTransactionsGroupViewModel> categoryGroupsCache,
            Dictionary<Guid, SubCategoryTransactionsGroupViewModel> subCategoryGroupsCache,
            BaseTransactionsGroupViewModel? selected, 
            Func<Transaction, BaseTransactionsGroupViewModel, bool> isDebet)
        where C : BaseCategoryViewModel, new() where S : BaseSubCategoryViewModel, new() 
    {
        if (!categoryGroupsCache.TryGetValue(transaction.CategoryId.GetValueOrDefault(), out var category))
        {
            category = new CategoryTransactionsGroupViewModel();
            if (transaction.CategoryId != null)
            {
                category.IsDebet = isDebet(transaction, category);
                category.Category = GetCategory<C>(transaction.CategoryId.Value);
                category.IsExpanded = _openedNodes.Contains(transaction.CategoryId.Value);
                category.IsSelected = selected is CategoryTransactionsGroupViewModel selectedCategory && selectedCategory!.Category?.Id == transaction.CategoryId;
                SelectedTransactionGroup = category.IsSelected ? category : SelectedTransactionGroup;
            }

            categoryGroupsCache.Add(transaction.CategoryId.GetValueOrDefault(), category);
            transactions.Add(category);
        }
        category.Sum += transaction.Sum;

        if (!subCategoryGroupsCache.TryGetValue(transaction.SubCategoryId.GetValueOrDefault(), out var subCategory))
        {
            subCategory = new SubCategoryTransactionsGroupViewModel();
            if (transaction.SubCategoryId != null)
            {
                subCategory.IsDebet = isDebet(transaction, subCategory);
                subCategory.SubCategory = GetSubCategory<S>(transaction.SubCategoryId.Value);
                subCategory.IsExpanded = _openedNodes.Contains(transaction.SubCategoryId.Value);
                subCategory.IsSelected = selected is SubCategoryTransactionsGroupViewModel selectedSubCategory 
                                        && selectedSubCategory!.SubCategory?.Id == transaction.SubCategoryId;
                SelectedTransactionGroup = subCategory.IsSelected ? subCategory : SelectedTransactionGroup;
            }

            subCategoryGroupsCache.Add(transaction.SubCategoryId.GetValueOrDefault(), subCategory);
            category.SubCategories.Add(subCategory);
        }
        subCategory.Sum += transaction.Sum;

        var viewTransaction = new TransactionGroupViewModel
        {
            Id = transaction.Id,
            Date = transaction.Date,
            LastChange = transaction.LastChange,
            Comment = transaction.Comment,
            Sum = transaction.Sum,
            Category = category.Category,
            SubCategory = subCategory.SubCategory,
        };
        viewTransaction.IsDebet = isDebet(transaction, viewTransaction);

        viewTransaction.IsSelected = selected is TransactionGroupViewModel selectedTransaction && selectedTransaction.Id == transaction.Id;
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

    private IList<BaseCategoryViewModel> GetCategories<T, N>() where T : Category where N : BaseCategoryViewModel, new()
    {
        var categories = _repository.GetCategories().OfType<T>().OrderBy(c => c.Name).Select(c =>
        {
            var category = new N();
            category.FillFrom(c, _repository);
            return category;
        });

        return categories.Select(c => (BaseCategoryViewModel)c).ToList();
    }

    private IList<BaseSubCategoryViewModel> GetSubCategories<T, N, C>() 
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
