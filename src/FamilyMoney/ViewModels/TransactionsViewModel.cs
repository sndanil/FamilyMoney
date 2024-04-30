using FamilyMoney.Csv;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.Utils;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Security.Principal;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class TransactionsViewModel : ViewModelBase
{
    private readonly IRepository _repository;
    private MainWindowViewModel? _mainWindowViewModel;

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
            var result = await TransactionViewModel.ShowDialog.Handle(transaction);
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
            var result = await TransactionViewModel.ShowDialog.Handle(transaction);
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
            var result = await TransactionViewModel.ShowDialog.Handle(transaction);
            if (result != null)
            {
            }
        });

        EditCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var transaction = new TransactionViewModel
            {
                FlatAccounts = GetFlatAccouunts(),
                Account = _mainWindowViewModel?.Accounts.SelectedAccount,
            };
            var result = await TransactionViewModel.ShowDialog.Handle(transaction);
            if (result != null)
            {
            }
        });

        DeleteCommand = ReactiveCommand.CreateFromTask(async () =>
        {
        });
    }

    private void RefreshTransactions(PeriodChangedMessage message)
    {
        var transactions = _repository.GetTransactions(message.From, message.To);
    }

    private IList<BaseCategoryViewModel> GetCategories<T, N>() where T : Category where N : BaseCategoryViewModel, new()
    {
        var categories = _repository.GetCategroties().OfType<T>().OrderBy(c => c.Name).Select(c => new N
        {
            Id = c.Id,
            Name = c.Name,
            Image = ImageConverter.ToImage(_repository.TryGetImage(c.Id)),
        });

        return categories.Select(c => (BaseCategoryViewModel)c).ToList();
    }

    private IList<BaseSubCategoryViewModel> GetSubCategories<T, N>() where T : SubCategory where N : BaseSubCategoryViewModel, new()
    { 
        var subCategories = _repository.GetSubCategroties().OfType<T>().OrderBy(c => c.Name).Select(c => new N
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
