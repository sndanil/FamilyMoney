using DynamicData;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.State;
using FamilyMoney.Utils;
using ReactiveUI;
using System;
using System.Collections.Generic;
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

    private HashSet<Guid> _openedNodes = new HashSet<Guid>();

    private TransactionsGroup _debetTransactions = new();
    private TransactionsGroup _creditTransactions = new();
    private TransactionsGroup _transferTransactions = new();

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
            .Subscribe(async m =>
            {
                DeleteTransaction(m.Element);
            });

        MessageBus.Current.Listen<TransactionGroupCopyMessage>()
            .Where(m => m.Element != null)
            .Subscribe(async m =>
            {
                await CopyTransaction(m.Element);
            });
    }

    private async Task AddTransferTransaction()
    {
        var transactionViewModel = CreateNewTransaction<TransferTransactionViewModel>(
            GetCategories<TransferCategory, TransferCategoryViewModel>(),
            GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel>(),
            SelectedTransactionGroup
            );

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
            GetSubCategories<CreditSubCategory, CreditSubCategoryViewModel>(),
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
            GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel>(),
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
                GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel>(),
                transactionGroupViewModel
                );
        }
        else if (transaction is CreditTransaction)
        {
            transactionViewModel = CreateNewTransaction<CreditTransactionViewModel>(
                GetCategories<CreditCategory, CreditCategoryViewModel>(),
                GetSubCategories<CreditSubCategory, CreditSubCategoryViewModel>(),
                transactionGroupViewModel
                );
        }
        else if (transaction is TransferTransaction)
        {
            transactionViewModel = CreateNewTransaction<TransferTransactionViewModel>(
                GetCategories<TransferCategory, TransferCategoryViewModel>(),
                GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel>(),
                transactionGroupViewModel
                );
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
        var flatAccounts = GetFlatAccouunts();
        var account = state.SelectedAccountId.HasValue ?
                            flatAccounts.FirstOrDefault(a => !a.IsGroup && (a.Id == state.SelectedAccountId || a.Parent?.Id == state.SelectedAccountId))
                            : flatAccounts.FirstOrDefault(a => !a.IsGroup);

        var transactionViewModel = new T()
        {
            FlatAccounts = flatAccounts,
            Account = account,
            Categories = categories,
            SubCategories = subCategories,
        };

        if (byGroup is CategoryTransactionsGroupViewModel categoryGroupViewModel)
        {
            transactionViewModel.Category = transactionViewModel.Categories.FirstOrDefault(c => c.Id == categoryGroupViewModel.Category?.Id);
        }

        if (byGroup is SubCategoryTransactionsGroupViewModel subCategoryGroupViewModel)
        {
            transactionViewModel.Category = transactionViewModel.Categories.FirstOrDefault(c => c.Id == subCategoryGroupViewModel.SubCategory?.CategoryId);
            transactionViewModel.SubCategory = subCategoryGroupViewModel.SubCategory;
        }

        if (byGroup is TransactionGroupViewModel transactionGroupViewModel)
        {
            var transaction = _repository.GetTransaction(transactionGroupViewModel.Id);

            transactionViewModel.Category = transactionViewModel.Categories.FirstOrDefault(c => c.Id == transaction?.CategoryId);
            transactionViewModel.SubCategory = transactionViewModel.SubCategories.FirstOrDefault(s => s.Id == transaction?.SubCategoryId);

            var ccategory = transactionViewModel.Categories.FirstOrDefault(c => c.Id == transactionViewModel.SubCategory?.CategoryId);
        }

        transactionViewModel.SubCategoryText = transactionViewModel.SubCategory?.Name;

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

        var flatAccounts = GetFlatAccouunts();
        if (transaction is DebetTransaction)
        {
            transactionViewModel = new DebetTransactionViewModel
            {
                Categories = GetCategories<DebetCategory, DebetCategoryViewModel>(),
                SubCategories = GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel>(),
            };
        }
        else if (transaction is CreditTransaction)
        {
            transactionViewModel = new CreditTransactionViewModel
            {
                Categories = GetCategories<CreditCategory, CreditCategoryViewModel>(),
                SubCategories = GetSubCategories<CreditSubCategory, CreditSubCategoryViewModel>(),
            };
        }
        else if (transaction is TransferTransaction transfer)
        {
            transactionViewModel = new TransferTransactionViewModel
            {
                Categories = GetCategories<TransferCategory, TransferCategoryViewModel>(),
                SubCategories = GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel>(),
                ToAccount = flatAccounts?.FirstOrDefault(a => a.Id == transfer.ToAccountId),
                ToSum = transfer.ToSum,
            };
        }
        else
        {
            return;
        }

        transactionViewModel.FillFrom(transaction, _repository);
        transactionViewModel.FlatAccounts = flatAccounts;
        transactionViewModel.Account = flatAccounts?.FirstOrDefault(a => a.Id == transaction.AccountId);
        transactionViewModel.Category = transactionViewModel.Categories.FirstOrDefault(c => c.Id == transaction.CategoryId);
        transactionViewModel.SubCategory = transactionViewModel.SubCategories.FirstOrDefault(c => c.Id == transaction.SubCategoryId);
        transactionViewModel.SubCategoryText = transactionViewModel.SubCategories.FirstOrDefault(c => c.Id == transaction.SubCategoryId)?.Name;

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

        var debetTransactions = new List<CategoryTransactionsGroupViewModel>();
        var creditTransactions = new List<CategoryTransactionsGroupViewModel>();
        var transferTransactions = new List<CategoryTransactionsGroupViewModel>();

        foreach (var transaction in transactions)
        {
            if (transaction is DebetTransaction)
            {
                FillTransactions<DebetCategoryViewModel, DebetSubCategoryViewModel>(debetTransactions, transaction, selected, true);
            }
            else if (transaction is CreditTransaction)
            {
                FillTransactions<CreditCategoryViewModel, CreditSubCategoryViewModel>(creditTransactions, transaction, selected);
            }
            else
            {
                FillTransactions<TransferCategoryViewModel, TransferSubCategoryViewModel>(transferTransactions, transaction, selected);
            }
        }

        DebetTransactions = new TransactionsGroup { Sum = debetTransactions.Sum(t => t.Sum), IsDebet = true };
        CalcPercents(DebetTransactions.Sum, debetTransactions);
        DebetTransactions.Categories.AddRange(debetTransactions);

        CreditTransactions = new TransactionsGroup { Sum = creditTransactions.Sum(t => t.Sum), IsDebet = false };
        CalcPercents(CreditTransactions.Sum, creditTransactions);
        CreditTransactions.Categories.AddRange(creditTransactions);

        TransferTransactions = new TransactionsGroup { Sum = transferTransactions.Sum(t => t.Sum) };
        CalcPercents(TransferTransactions.Sum, transferTransactions);
        TransferTransactions.Categories.AddRange(transferTransactions);

        Total = DebetTransactions.Sum - CreditTransactions.Sum;
    }

    private void CalcPercents(decimal sum, IEnumerable<CategoryTransactionsGroupViewModel> categories)
    {
        foreach (var category in categories)
        {
            category.Percent = Math.Round(category.Sum / sum, 2, MidpointRounding.AwayFromZero);
            foreach (var subCategory in category.SubCategories)
            {
                subCategory.Percent = Math.Round(subCategory.Sum / sum, 2, MidpointRounding.AwayFromZero);
                foreach (var transaction in subCategory.Transactions)
                {
                    transaction.Percent = Math.Round(transaction.Sum / sum, 2, MidpointRounding.AwayFromZero);
                }
            }
        }
    }

    private void FillTransactions<C, S>(List<CategoryTransactionsGroupViewModel> transactions, 
            Transaction transaction, 
            BaseTransactionsGroupViewModel? selected, 
            bool isDebet = false)
        where C : BaseCategoryViewModel, new() where S : BaseSubCategoryViewModel, new() 
    {
        var category = transactions.FirstOrDefault(t => t.Category?.Id == transaction.CategoryId);
        if (category == null)
        {
            category = new CategoryTransactionsGroupViewModel { IsDebet = isDebet };
            if (transaction.CategoryId != null)
            {
                category.Category = new C();
                category.IsExpanded = _openedNodes.Contains(transaction.CategoryId.Value);
                category.Category.FillFrom(transaction.CategoryId.Value, _repository);
                category.IsSelected = selected is CategoryTransactionsGroupViewModel selectedCategory && selectedCategory!.Category?.Id == transaction.CategoryId;
                SelectedTransactionGroup = category.IsSelected ? category : SelectedTransactionGroup;
            }

            transactions.Add(category);
        }
        category.Sum += transaction.Sum;

        var subCategory = category.SubCategories.FirstOrDefault(c => c.SubCategory?.Id == transaction.SubCategoryId);
        if (subCategory == null)
        {
            subCategory = new SubCategoryTransactionsGroupViewModel { IsDebet = isDebet };
            if (transaction.SubCategoryId != null)
            {
                subCategory.SubCategory = new S();
                subCategory.IsExpanded = _openedNodes.Contains(transaction.SubCategoryId.Value);
                subCategory.SubCategory.FillFrom(transaction.SubCategoryId.Value, _repository);
                subCategory.IsSelected = selected is SubCategoryTransactionsGroupViewModel selectedSubCategory 
                                        && selectedSubCategory!.SubCategory?.Id == transaction.SubCategoryId;
                SelectedTransactionGroup = subCategory.IsSelected ? subCategory : SelectedTransactionGroup;
            }

            category.SubCategories.Add(subCategory);
        }
        subCategory.Sum += transaction.Sum;

        var viewTransaction = new TransactionGroupViewModel
        {
            Id = transaction.Id,
            Date = transaction.Date,
            Comment = transaction.Comment,
            Sum = transaction.Sum,
            IsDebet = isDebet,
        };
        viewTransaction.IsSelected = selected is TransactionGroupViewModel selectedTransaction && selectedTransaction.Id == transaction.Id;
        SelectedTransactionGroup = viewTransaction.IsSelected ? viewTransaction : SelectedTransactionGroup;

        subCategory.Transactions.Add(viewTransaction);
    }

    private IList<BaseCategoryViewModel> GetCategories<T, N>() where T : Category where N : BaseCategoryViewModel, new()
    {
        var categories = _repository.GetCategories().OfType<T>().OrderBy(c => c.Name).Select(c => new N
        {
            Id = c.Id,
            Name = c.Name,
            Image = ImageConverter.ToImage(_repository.TryGetImage(c.Id)),
        });

        return categories.Select(c => (BaseCategoryViewModel)c).ToList();
    }

    private IList<BaseSubCategoryViewModel> GetSubCategories<T, N>() where T : SubCategory where N : BaseSubCategoryViewModel, new()
    {
        var subCategories = _repository.GetSubCategories().OfType<T>().OrderBy(c => c.Name).Select(c => new N
        {
            Id = c.Id,
            Name = c.Name,
            CategoryId = c.CategoryId,
        });

        return subCategories.Select(c => (BaseSubCategoryViewModel)c).ToList();
    }

    private IList<AccountViewModel> GetFlatAccouunts()
    {
        var state = _stateManager.GetMainState();
        var result = new List<AccountViewModel>();
        foreach (var account in state.Accounts)
        {
            result.Add(account);
            foreach (var childAccount in account.Children)
            {
                result.Add(childAccount);
            }
        }

        return result;
    }
}
