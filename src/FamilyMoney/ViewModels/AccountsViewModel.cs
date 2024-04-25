using FamilyMoney.DataAccess;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public class AccountsViewModel : ViewModelBase
{
    private AccountViewModel? _total = null;
    private AccountViewModel? _selectedAccount = null;
    private AccountViewModel? _draggingAccount = null;

    private IRepository _repository;

    public AccountsViewModel(IRepository repository)
    {
        _repository = repository;

        RxApp.MainThreadScheduler.Schedule(LoadAccounts);
    }

    public AccountViewModel? Total
    {
        get => _total;
        set => this.RaiseAndSetIfChanged(ref _total, value);
    }

    public AccountViewModel? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            Total!.IsSelected = false;
            foreach (var group in Total.Children)
            {
                group.IsSelected = false;
                foreach (var account in group.Children)
                {
                    account.IsSelected = false;
                }
            }

            value!.IsSelected = true;
            this.RaiseAndSetIfChanged(ref _selectedAccount, value);
        }
    }

    public AccountViewModel? DraggingAccount
    {
        get => _draggingAccount;
        set => this.RaiseAndSetIfChanged(ref _draggingAccount, value);
    }

    private void LoadAccounts()
    {
        _total = new AccountViewModel
        {
            Name = "Всего",
            Amount = 2000,
        };

        var accounts = _repository!.GetAccounts();
        _total.AddFromAccount(_repository, accounts);
    }

}
