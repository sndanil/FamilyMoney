using DynamicData;
using FamilyMoney.Csv;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.State;
using FamilyMoney.Utils;
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
    private MainWindowViewModel? _mainWindowViewModel;
    private decimal _total = 0m;

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

    public ICommand DeleteCommand { get; }

    public MainWindowViewModel? MainWindowViewModel
    {
        get => _mainWindowViewModel;
        set => this.RaiseAndSetIfChanged(ref _mainWindowViewModel, value);
    }

    public decimal Total
    {
        get => _total;
        set => this.RaiseAndSetIfChanged(ref _total, value);
    }

    public TransactionsViewModel(IRepository repository, IStateManager stateManager)
    {
        _repository = repository;
        _stateManager = stateManager;

        MessageBus.Current.Listen<MainStateChangedMessage>()
            .Where(m => m.State != null)
            .Subscribe(m => RefreshTransactions(m.State));

        MessageBus.Current.Listen<TransactionGroupSelectMessage>()
            .Where(m => m.Element != null)
            .Subscribe(m =>
            {
                ClearSelection();
                _selectedTransactionGroup = m.Element;
                _selectedTransactionGroup.IsSelected = true;
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

        AddDebetCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var state = _stateManager.GetMainState();
            var flatAccounts = GetFlatAccouunts();
            var account = state.SelectedAccountId.HasValue ? 
                                flatAccounts?.FirstOrDefault(a => !a.IsGroup && (a.Id == state.SelectedAccountId || a.Parent?.Id == state.SelectedAccountId)) 
                                : flatAccounts?.FirstOrDefault(a => !a.IsGroup);

            var transactionViewModel = new DebetTransactionViewModel
            {
                FlatAccounts = flatAccounts,
                Account = account,
                Categories = GetCategories<DebetCategory, DebetCategoryViewModel>(),
                SubCategories = GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel>(),
            };

            if (_selectedTransactionGroup is CategoryTransactionsGroupViewModel categoryGroupViewModel)
            {
                transactionViewModel.Category = transactionViewModel.Categories.FirstOrDefault(c => c.Id == categoryGroupViewModel.Category?.Id);
            }

            if (_selectedTransactionGroup is SubCategoryTransactionsGroupViewModel subCategoryGroupViewModel)
            {
                transactionViewModel.Category = transactionViewModel.Categories.FirstOrDefault(c => c.Id == subCategoryGroupViewModel.SubCategory?.CategoryId);
                transactionViewModel.SubCategory = subCategoryGroupViewModel.SubCategory?.Name;
            }

            if (_selectedTransactionGroup is TransactionGroupViewModel transactionGroupViewModel)
            {
                var transaction = _repository.GetTransaction(transactionGroupViewModel.Id);

                transactionViewModel.Category = transactionViewModel.Categories.FirstOrDefault(c => c.Id == transaction?.CategoryId);
                transactionViewModel.SubCategory = transactionViewModel.SubCategories.FirstOrDefault(s => s.Id == transaction?.SubCategoryId)?.Name;
            }

            var result = await BaseTransactionViewModel.ShowDialog.Handle(transactionViewModel);
            if (result != null)
            {
                SaveTransaction(new DebetTransaction { Id = Guid.NewGuid() }, result);
            }
        });

        AddCreditCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var transaction = new CreditTransactionViewModel
            {
                FlatAccounts = GetFlatAccouunts(),
                Account = _mainWindowViewModel?.Accounts.SelectedAccount,
                Categories = GetCategories<CreditCategory, CreditCategoryViewModel>(),
                SubCategories = GetSubCategories<CreditSubCategory, CreditSubCategoryViewModel>(),
            };
            var result = await BaseTransactionViewModel.ShowDialog.Handle(transaction);
            if (result != null)
            {
                SaveTransaction(new CreditTransaction { Id = Guid.NewGuid() }, result);
            }
        });

        AddTransferCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var transaction = new TransferTransactionViewModel
            {
                FlatAccounts = GetFlatAccouunts(),
                Account = _mainWindowViewModel?.Accounts.SelectedAccount,
                Categories = GetCategories<TransferCategory, TransferCategoryViewModel>(),
                SubCategories = GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel>(),
            };
            var result = await BaseTransactionViewModel.ShowDialog.Handle(transaction);
            if (result != null)
            {
                SaveTransaction(new TransferTransaction { Id = Guid.NewGuid() }, result);
            }
        });

        EditCommand = ReactiveCommand.CreateFromTask(EditTransaction);

        DeleteCommand = ReactiveCommand.CreateFromTask(async () =>
        {
        });
    }

    private async Task EditTransaction()
    {
        if (_selectedTransactionGroup is not TransactionGroupViewModel transactionGroupViewModel)
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
        transactionViewModel.SubCategory = transactionViewModel.SubCategories.FirstOrDefault(c => c.Id == transaction.SubCategoryId)?.Name;

        if (transactionViewModel == null)
        {
            return;
        }

        var result = await BaseTransactionViewModel.ShowDialog.Handle(transactionViewModel);
        if (result != null)
        {
            SaveTransaction(transaction, result);

            RefreshTransactions(_stateManager.GetMainState());
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

        if (!string.IsNullOrEmpty(transactionViewModel.SubCategory))
        {
            Func<SubCategory> factory = () => new DebetSubCategory
            {
                Id = Guid.NewGuid(),
                Name = transactionViewModel.SubCategory!,
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
                    Name = transactionViewModel.SubCategory!,
                    CategoryId = transaction.CategoryId
                };
            }
            else if (transaction is TransferTransaction transferTransaction)
            {
                factory = () => new TransferSubCategory
                {
                    Id = Guid.NewGuid(),
                    Name = transactionViewModel.SubCategory!,
                    CategoryId = transaction.CategoryId
                };

                transferTransaction.ToAccountId = transactionViewModel.ToAccount?.Id;
                transferTransaction.ToSum = transactionViewModel.ToSum;
            }

            transaction.SubCategoryId = _repository.GetOrCreateSubCategory(transaction.CategoryId, transactionViewModel.SubCategory!, factory).Id;
        }

        _repository.UpdateTransaction(transaction);
    }

    private void ClearSelection()
    {
        if (_selectedTransactionGroup != null)
        {
            _selectedTransactionGroup.IsSelected = false;
            _selectedTransactionGroup = null;
        }
    }

    private void RefreshTransactions(MainState state)
    {
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
                FillTransactions<DebetCategoryViewModel, DebetSubCategoryViewModel, TransactionGroupViewModel>(debetTransactions, transaction, true);
            }
            else if (transaction is CreditTransaction)
            {
                FillTransactions<CreditCategoryViewModel, CreditSubCategoryViewModel, TransactionGroupViewModel>(creditTransactions, transaction);
            }
            else
            {
                FillTransactions<TransferCategoryViewModel, TransferSubCategoryViewModel, TransactionGroupViewModel>(transferTransactions, transaction);
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

    private void FillTransactions<C, S, T>(List<CategoryTransactionsGroupViewModel> transactions, Transaction transaction, bool isDebet = false)
        where C : BaseCategoryViewModel, new() where S : BaseSubCategoryViewModel, new() where T : TransactionGroupViewModel, new()
    {
        var category = transactions.FirstOrDefault(t => t.Category?.Id == transaction.CategoryId);
        if (category == null)
        {
            category = new CategoryTransactionsGroupViewModel { IsDebet = isDebet };
            if (transaction.CategoryId != null)
            {
                category.Category = new C();
                category.Category.FillFrom(transaction.CategoryId.Value, _repository);
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
                subCategory.SubCategory.FillFrom(transaction.SubCategoryId.Value, _repository);
            }

            category.SubCategories.Add(subCategory);
        }
        subCategory.Sum += transaction.Sum;

        var viewTransaction = new T()
        {
            Id = transaction.Id,
            Date = transaction.Date,
            Comment = transaction.Comment,
            Sum = transaction.Sum,
            IsDebet = isDebet,
        };

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

    private IList<AccountViewModel>? GetFlatAccouunts()
    {
        var result = new List<AccountViewModel>();
        if (MainWindowViewModel != null)
        {
            foreach (var account in MainWindowViewModel.Accounts.Total.Children)
            {
                result.Add(account);
                foreach (var childAccount in account.Children)
                {
                    result.Add(childAccount);
                }
            }
        }

        return result;
    }
}
