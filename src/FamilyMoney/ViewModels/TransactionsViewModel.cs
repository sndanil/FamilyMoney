using DynamicData;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
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

    private TransactionsGroup _debetTransactions = new ();
    private TransactionsGroup _creditTransactions = new();
    private TransactionsGroup _transferTransactions = new();

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

    public TransactionsViewModel(IRepository repository)
    {
        _repository = repository;

        MessageBus.Current.Listen<PeriodChangedMessage>()
            .Where(p => p.To != DateTime.MinValue)
            .Subscribe(x => RefreshTransactions(x));

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
            //var transaction = new BaseTransactionViewModel
            //{
            //    FlatAccounts = GetFlatAccouunts(),
            //    Account = _mainWindowViewModel?.Accounts.SelectedAccount,
            //};
            //var result = await BaseTransactionViewModel.ShowDialog.Handle(transaction);
            //if (result != null)
            //{
            //}
        });

        DeleteCommand = ReactiveCommand.CreateFromTask(async () =>
        {
        });
    }

    private void RefreshTransactions(PeriodChangedMessage message)
    {
        var transactions = _repository.GetTransactions(message.From, message.To);

        var debetTransactions = new List<CategoryTransactionsGroupViewModel>();
        var creditTransactions = new List<CategoryTransactionsGroupViewModel>();
        var transferTransactions = new List<CategoryTransactionsGroupViewModel>();
        
        foreach (var transaction in transactions)
        {
            if (transaction is DebetTransaction)
            {
                FillTransactions<DebetCategoryViewModel, DebetSubCategoryViewModel, DebetTransactionViewModel>(debetTransactions, transaction);
            }
            else if (transaction is CreditTransaction)
            {
                FillTransactions<CreditCategoryViewModel, CreditSubCategoryViewModel, CreditTransactionViewModel>(creditTransactions, transaction);
            }
            else
            {
                FillTransactions<TransferCategoryViewModel, TransferSubCategoryViewModel, TransferTransactionViewModel>(transferTransactions, transaction);
            }
        }

        DebetTransactions = new TransactionsGroup { Sum = debetTransactions.Sum(t => t.Sum) };
        DebetTransactions.Categories.AddRange(debetTransactions);

        CreditTransactions = new TransactionsGroup { Sum = creditTransactions.Sum(t => t.Sum) };
        CreditTransactions.Categories.AddRange(creditTransactions);

        TransferTransactions = new TransactionsGroup { Sum = transferTransactions.Sum(t => t.Sum) };
        TransferTransactions.Categories.AddRange(transferTransactions);
    }

    private void FillTransactions<C, S, T>(List<CategoryTransactionsGroupViewModel> transactions, Transaction transaction) 
        where C: BaseCategoryViewModel, new() where S: BaseSubCategoryViewModel, new() where T: BaseTransactionViewModel, new()
    {
        var category = transactions.FirstOrDefault(t => t.Category?.Id == transaction.CategoryId);
        if (category == null)
        {
            category = new CategoryTransactionsGroupViewModel();
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
            subCategory = new SubCategoryTransactionsGroupViewModel();
            if (transaction.SubCategoryId != null)
            {
                subCategory.SubCategory = new S();
                subCategory.SubCategory.FillFrom(transaction.SubCategoryId.Value, _repository);
            }

            category.SubCategories.Add(subCategory);
        }
        subCategory.Sum += transaction.Sum;

        var viewTransaction = new T();
        viewTransaction.FillFrom(transaction, _repository);

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
