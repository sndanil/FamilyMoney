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
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class TransactionsViewModel : ViewModelBase
{
    private readonly IRepository _repository;
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

    public TransactionsViewModel(IRepository repository)
    {
        _repository = repository;

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
            //(new CsvImporter()).DoImport(_repository);
            //return;

            var transaction = new DebetTransactionViewModel
            {
                FlatAccounts = GetFlatAccouunts(),
                Account = _mainWindowViewModel?.Accounts.SelectedAccount,
                Categories = GetCategories<DebetCategory, DebetCategoryViewModel>(),
                SubCategories = GetSubCategories<DebetSubCategory, DebetSubCategoryViewModel>(),
            };
            var result = await BaseTransactionViewModel.ShowDialog.Handle(transaction);
            if (result != null)
            {
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
            }
        });

        EditCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            BaseTransactionViewModel transactionViewModel = null;
            if (_selectedTransactionGroup is TransactionGroupViewModel transactionGroupViewModel)
            {
                var transaction = _repository.GetTransaction(transactionGroupViewModel.Id);
                if (transaction != null)
                {
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
                    else
                    {
                        transactionViewModel = new TransferTransactionViewModel
                        {
                            Categories = GetCategories<TransferCategory, TransferCategoryViewModel>(),
                            SubCategories = GetSubCategories<TransferSubCategory, TransferSubCategoryViewModel>(),
                        };
                    }

                    transactionViewModel.FillFrom(transaction, _repository);
                    var flatAccounts = GetFlatAccouunts();
                    transactionViewModel.FlatAccounts = flatAccounts;
                    transactionViewModel.Account = flatAccounts?.FirstOrDefault(a => a.Id == transaction.AccountId);
                    transactionViewModel.Category = transactionViewModel.Categories.FirstOrDefault(c => c.Id == transaction.CategoryId);
                    transactionViewModel.SubCategory = transactionViewModel.SubCategories.FirstOrDefault(c => c.Id == transaction.SubCategoryId)?.Name;
                }
            }

            if (transactionViewModel == null)
            {
                return;
            }
            
            var result = await BaseTransactionViewModel.ShowDialog.Handle(transactionViewModel);
            if (result != null)
            {
            }
        });

        DeleteCommand = ReactiveCommand.CreateFromTask(async () =>
        {
        });
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
        where C: BaseCategoryViewModel, new() where S: BaseSubCategoryViewModel, new() where T: TransactionGroupViewModel, new()
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
