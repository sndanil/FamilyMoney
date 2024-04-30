using FamilyMoney.Csv;
using FamilyMoney.DataAccess;
using ReactiveUI;
using System.Collections.Generic;
using System.Reactive.Linq;
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

        AddDebetCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            (new CsvImporter()).DoImport(_repository);

            //var transaction = new TransactionViewModel 
            //{ 
            //    FlatAccounts = GetFlatAccouunts(),
            //    Account = _mainWindowViewModel?.Accounts.SelectedAccount,
            //};
            //var result = await TransactionViewModel.ShowDialog.Handle(transaction);
            //if (result != null)
            //{
            //}
        });

        AddCreditCommand = ReactiveCommand.CreateFromTask(async () =>
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

        AddTransferCommand = ReactiveCommand.CreateFromTask(async () =>
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
