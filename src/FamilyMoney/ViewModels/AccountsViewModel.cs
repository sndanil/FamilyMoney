using Avalonia.Media.Imaging;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.State;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class AccountsViewModel : ViewModelBase
{
    private readonly AccountViewModel _total = new() 
    {
        Name = "Всего",
        IsGroup = true,
    };

    private AccountViewModel? _draggingAccount = null;

    private bool _showHidden = false;

    private readonly IRepository _repository;
    private readonly IStateManager _stateManager;

    public ICommand AddGroupCommand { get; }

    public ICommand AddElementCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand ShowHiddenCommand { get; }

    public ICommand PrevAccount { get; }

    public ICommand NextAccount { get; }

    public AccountViewModel Total
    {
        get => _total;
    }

    public AccountViewModel? DraggingAccount
    {
        get => _draggingAccount;
        set => this.RaiseAndSetIfChanged(ref _draggingAccount, value);
    }

    public bool ShowHidden
    {
        get => _showHidden;
        set => this.RaiseAndSetIfChanged(ref _showHidden, value);
    }

    public AccountViewModel? SelectedAccount
    {
        get
        {
            var state = _stateManager.GetMainState();
            if (state.SelectedAccountId != null)
            {
                var found = state.FlatAccounts.FirstOrDefault(a => a.Id == state.SelectedAccountId);
                return found;
            }

            return _total;
        }
        set
        {
            MessageBus.Current.SendMessage(new AccountSelectMessage(value?.Id));
        }
    }

    public AccountsViewModel(IRepository repository, IStateManager stateManager)
    {
        _repository = repository;
        _stateManager = stateManager;

        AddGroupCommand = ReactiveCommand.CreateFromTask(AddGroupAccount);
        AddElementCommand = ReactiveCommand.CreateFromTask(AddElementAccount);
        EditCommand = ReactiveCommand.CreateFromTask<AccountViewModel>(EditAccount);

        ShowHiddenCommand = ReactiveCommand.CreateFromTask(() =>
        {
            ShowHidden = !ShowHidden;
            return Task.CompletedTask;
        });

        NextAccount = ReactiveCommand.Create(() =>
        {
            SelectedAccount ??= Total;

            var flatAccounts = _stateManager.GetMainState().FlatAccounts.Where(a => ShowHidden || !a.IsHidden).ToList();
            var index = flatAccounts.IndexOf(SelectedAccount);
            if (index < flatAccounts.Count - 1)
            {
                SelectedAccount = flatAccounts[index + 1];
            }
            else
            {
                SelectedAccount = Total;
            }
        });

        PrevAccount = ReactiveCommand.Create(() =>
        {
            SelectedAccount ??= Total;

            var flatAccounts = _stateManager.GetMainState().FlatAccounts.Where(a => ShowHidden || !a.IsHidden).ToList();
            var index = flatAccounts.IndexOf(SelectedAccount);
            if (index == 0)
            {
                SelectedAccount = Total;
            }
            else if (index > 0)
            {
                SelectedAccount = flatAccounts[index - 1];
            }
            else
            {
                SelectedAccount = flatAccounts[^1];
            }
        });

        SubscribeMessages();
    }

    public IReadOnlyList<AccountViewModel> LoadAccounts()
    {
        _total.Children.Clear();
        var accounts = _repository!.GetAccounts();
        _total.AddFromAccount(_repository, accounts);

        return [.. _total.Children];
    }

    private void RecalcAccounts()
    {
        foreach (var account in Total.Children)
        {
            account.RecalcByChildren();
        }

        Total.RecalcByChildren();
    }

    private void SubscribeMessages()
    {
        MessageBus.Current.Listen<MainStateChangedMessage>()
            .Where(m => m.State != null)
            .Subscribe(m => RefreshSelectedAccount(m.State.SelectedAccountId));

        MessageBus.Current.Listen<AccountSelectMessage>()
            .Where(m => m != null)
            .Subscribe(m =>
            {
                var state = _stateManager.GetMainState();
                _stateManager.SetMainState(state with { SelectedAccountId = m.AccountId });

                this.RaisePropertyChanged(nameof(SelectedAccount));
            });

        MessageBus.Current.Listen<TransactionChangedMessage>()
            .Where(m => m != null)
            .Subscribe(m =>
            {
                if (m.Before != null)
                {
                    ProcessAccounts(m.Before, -1);
                }
                if (m.After != null)
                {
                    ProcessAccounts(m.After, 1);
                }
            });
    }

    private void ProcessAccounts(Transaction transaction, int direction)
    {
        var flatAccounts = _stateManager.GetMainState().FlatAccounts;
        var account = flatAccounts.FirstOrDefault(a => a.Id == transaction.AccountId);
        if (account != null)
        {
            if (transaction is DebetTransaction)
            {
                UpdateAccountSum(account, transaction.Sum * direction);
            }
            else if (transaction is CreditTransaction)
            {
                UpdateAccountSum(account, -transaction.Sum * direction);
            }
            else if (transaction is TransferTransaction transfer)
            {
                UpdateAccountSum(account, -transaction.Sum * direction);
                var toAccount = flatAccounts.FirstOrDefault(a => a.Id == transfer.ToAccountId);
                if (toAccount != null)
                {
                    UpdateAccountSum(toAccount, transfer.ToSum * direction);
                }
            }
        }
    }

    private void UpdateAccountSum(AccountViewModel account, decimal sum)
    {
        account.Sum += sum;
        if (!account.IsGroup)
        {
            Save(null, account);
        }
    }

    private async Task AddGroupAccount()
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
    }

    private async Task AddElementAccount()
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
    }

    private async Task EditAccount(AccountViewModel editAccount)
    {
        if (editAccount == Total || editAccount == null)
        {
            return;
        }

        var account = new AccountViewModel()
        {
            Id = editAccount!.Id,
            Parent = editAccount.Parent,
            Name = editAccount.Name,
            Sum = editAccount.Sum,
            IsGroup = editAccount.IsGroup,
            Image = editAccount.Image,
            IsHidden = editAccount.IsHidden,
            IsNotSummable = editAccount.IsNotSummable,
        };
        var result = await AccountViewModel.ShowDialog.Handle(account);
        if (result != null)
        {
            Save(editAccount, result);
        }
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

    private void RefreshSelectedAccount(Guid? accountId)
    {
        Total!.IsSelected = !accountId.HasValue;
        foreach (var group in Total.Children)
        {
            group.IsSelected = group.Id == accountId;
            foreach (var account in group.Children)
            {
                account.IsSelected = account.Id == accountId;
            }
        }
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
            one.IsHidden = other.IsHidden;
            one.IsNotSummable = other.IsNotSummable;
        }

        var state = _stateManager.GetMainState();
        _stateManager.SetMainState(state with { Accounts = [.. Total.Children] });
        _repository!.UpdateAccount(new Models.Account
        {
            Id = other.Id.Value,
            ParentId = other.Parent?.Id,
            Name = other.Name,
            IsGroup = other.IsGroup,
            IsHidden = other.IsHidden,
            IsNotSummable = other.IsNotSummable,
            Sum = other.IsGroup ? 0 : other.Sum,
            Order = (other.Parent ?? Total).Children
                        .Select((account, index) => (account, index))
                        .Where(i => i.account.Id == other.Id)
                        .Select(i => i.index)
                        .First(),
        });

        RecalcAccounts();
    }

    private void UpdateChildren(AccountViewModel account)
    {
        foreach (var child in account.Children)
        {
            Save(child, child);
        }
    }
}
