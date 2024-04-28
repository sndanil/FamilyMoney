﻿using Avalonia.Media.Imaging;
using FamilyMoney.DataAccess;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class AccountsViewModel : ViewModelBase
{
    private AccountViewModel _total = new AccountViewModel();
    private AccountViewModel? _selectedAccount = null;
    private AccountViewModel? _draggingAccount = null;

    private readonly IRepository _repository;

    public ICommand AddGroupCommand { get; }

    public ICommand AddElementCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }

    public AccountsViewModel(IRepository repository)
    {
        _repository = repository;

        AddGroupCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var account = new AccountViewModel { IsGroup = true };
            var result = await AccountViewModel.ShowDialog.Handle(account);
            if (result != null)
            {
                Total.Children.Add(result);
                result.Parent = Total;
                result.Id = Guid.NewGuid();
                Save(null, result);
            }
        });

        AddElementCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var account = new AccountViewModel();
            var result = await AccountViewModel.ShowDialog.Handle(account);
            if (result != null)
            {
                var parent = SelectedAccount?.IsGroup == true ? SelectedAccount : (SelectedAccount?.Parent ?? Total);
                parent.Children.Add(result);
                result.Id = Guid.NewGuid();
                result.Parent = parent;
                Save(null, result);
            }
        });

        var canEditExecute = this.WhenAnyValue(x => x.SelectedAccount, x => x.Total,
            (selectedAccount, total) => selectedAccount != null && selectedAccount != total);
        EditCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var account = new AccountViewModel()
            {
                Id = SelectedAccount!.Id,
                Parent = SelectedAccount.Parent,
                Name = SelectedAccount.Name,
                Image = SelectedAccount.Image,
            };
            var result = await AccountViewModel.ShowDialog.Handle(account);
            if (result != null)
            {
                Save(SelectedAccount, result);
            }
        }, canEditExecute);

        DeleteCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            _repository.DeleteAccount(SelectedAccount!.Id!.Value);
            SelectedAccount!.Parent!.Children.Remove(SelectedAccount);
            UpdateChildren(SelectedAccount.Parent);
        }, canEditExecute);

        RxApp.MainThreadScheduler.Schedule(LoadAccounts);
    }

    public AccountViewModel Total
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
            IsGroup = true,
            Amount = 0,
        };

        var accounts = _repository!.GetAccounts();
        _total.AddFromAccount(_repository, accounts);
    }

    private void Save(AccountViewModel? one, AccountViewModel other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));
        ArgumentNullException.ThrowIfNull(other.Id, $"{nameof(other)}.{nameof(other.Id)}");

        if (one?.Image != other.Image)
        {
            var stream = new MemoryStream();
            ((Bitmap)other.Image!).Save(stream);
            stream.Position = 0;
            _repository!.UpdateImage(other.Id.Value, other.Name, stream);
        }

        if (one != null)
        {
            one.Name = other.Name;
            one.Image = other.Image;
        }

        _repository!.UpdateAccount(new Models.Account
        {
            Id = other.Id.Value,
            ParentId = other.Parent?.Id,
            Name = other.Name,
            IsGroup = other.IsGroup,
            Order = (other.Parent ?? Total).Children
                        .Select((account, index) => (account, index))
                        .Where(i => i.account.Id == other.Id)
                        .Select(i => i.index)
                        .First(),
        });
    }

    public bool IsDestinationValid(AccountViewModel account, AccountViewModel destAccount)
    {
        if (account.IsGroup && !destAccount.IsGroup && destAccount.Parent != Total) 
        {
            return false;
        }

        return true;
    }

    public void Drop(AccountViewModel account, AccountViewModel destAccount)
    {
        if (account == destAccount) 
        {
            return;
        }

        if (account.IsGroup && (destAccount.IsGroup && destAccount != Total || !destAccount.IsGroup && destAccount.Parent == Total))
        {
            var index = Total.Children.IndexOf(account);
            var destIndex = Total.Children.IndexOf(destAccount);
            if (index < destIndex)
            {
                Total.Children.Insert(destIndex + 1, account);
                Total.Children.RemoveAt(index);
            }
            else
            {
                Total.Children.Insert(destIndex, account);
                Total.Children.RemoveAt(index + 1);
            }

            UpdateChildren(Total);
        }

        if (account.IsGroup && destAccount == Total)
        {
            Total.Children.Remove(account);
            Total.Children.Insert(0, account);

            UpdateChildren(Total);
        }

        if (!account.IsGroup && destAccount.IsGroup)
        {
            var oldParent = account.Parent!;
            account.Parent!.Children.Remove(account);
            destAccount.Children.Insert(0, account);
            account.Parent = destAccount;

            UpdateChildren(oldParent);
            UpdateChildren(destAccount);
        }

        if (!account.IsGroup && !destAccount.IsGroup)
        {
            var oldParent = account.Parent!;
            var destIndex = destAccount.Parent!.Children.IndexOf(destAccount);

            if (account.Parent == destAccount.Parent)
            {
                var index = account.Parent!.Children.IndexOf(account);
                if (index < destIndex)
                {
                    destAccount.Parent!.Children.Insert(destIndex + 1, account);
                    account.Parent!.Children.RemoveAt(index);
                }
                else
                {
                    destAccount.Parent!.Children.Insert(destIndex, account);
                    account.Parent!.Children.RemoveAt(index + 1);
                }
            }
            else
            {
                account.Parent!.Children.Remove(account);
                destAccount.Parent!.Children.Insert(destIndex, account);
            }
            account.Parent = destAccount.Parent!;

            UpdateChildren(oldParent);
            UpdateChildren(destAccount.Parent!);
        }
    }

    private void UpdateChildren(AccountViewModel account)
    {
        foreach (var child in account.Children)
        {
            Save(child, child);
        }
    }
}
