using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.State;
using FamilyMoney.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public partial class AccountsViewModel : ViewModelBase
{
    private readonly AccountViewModel _total = new()
    {
        Name = "Всего",
        IsGroup = true,
    };

    private readonly IRepository _repository;
    private readonly IStateManager _stateManager;
    private readonly IAccountLocalSettingsStore _localSettings;

    [ObservableProperty]
    public partial AccountViewModel? DraggingAccount { get; set; }

    [ObservableProperty]
    public partial bool ShowHidden { get; set; }

    [ObservableProperty]
    public partial bool ShowHiddenReorder { get; set; }

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
            WeakReferenceMessenger.Default.Send(new AccountSelectMessage(value?.Id));
        }
    }

    public AccountsViewModel(IRepository repository, IStateManager stateManager, IAccountLocalSettingsStore localSettings)
    {
        _repository = repository;
        _stateManager = stateManager;
        _localSettings = localSettings;

        SubscribeMessages();
    }

    public AccountViewModel Total
    {
        get => _total;
    }

    [RelayCommand]
    public async Task ShowHiddenAsync()
    {
        ShowHidden = !ShowHidden;
    }

    [RelayCommand]
    public async Task ReorderAsync()
    {
        ShowHiddenReorder = !ShowHiddenReorder;
    }

    [RelayCommand]
    public async Task NextAccountAsync()
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
    }

    [RelayCommand]
    public async Task PrevAccountAsync()
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
    }

    public IReadOnlyList<AccountViewModel> LoadAccounts()
    {
        _repository!.RecalculateAccountBalances();
        _total.Children.Clear();
        var accounts = _repository!.GetAccounts();
        _total.AddFromAccount(_repository, _localSettings, accounts);

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
        WeakReferenceMessenger.Default.Register<AccountsViewModel, MainStateChangedMessage>(this, (a, m) =>
        {
            if (m?.State != null)
            {
                RefreshSelectedAccount(m.State.SelectedAccountId);
            }
        });

        WeakReferenceMessenger.Default.Register<AccountsViewModel, AccountSelectMessage>(this, (a, m) =>
        {
            if (m != null)
            {
                var state = _stateManager.GetMainState();
                _stateManager.SetMainState(state with { SelectedAccountId = m.AccountId });
            }
        });

        WeakReferenceMessenger.Default.Register<AccountsViewModel, AccountExpandMessage>(this, (a, m) =>
        {
            // Свёрнутость — локальная настройка устройства, в базу не пишем.
            if (m?.AccountId != null)
            {
                a._localSettings.SetExpanded(m.AccountId.Value, m.IsExpanded);
            }
        });

        WeakReferenceMessenger.Default.Register<AccountsViewModel, AccountHideMessage>(this, (a, m) =>
        {
            if (m?.AccountId != null)
            {
                a._localSettings.SetHidden(m.AccountId.Value, m.IsHidden);
            }
        });

        WeakReferenceMessenger.Default.Register<AccountsViewModel, TransactionChangedMessage>(this, (a, m) =>
        {
            if (m != null)
            {
                a._repository.RecalculateAccountBalances();
                a.RefreshAccountSumsFromRepository();
            }
        });
    }

    private void RefreshAccountSumsFromRepository()
    {
        var accounts = _repository.GetAccounts().ToDictionary(a => a.Id);

        void UpdateRecursive(AccountViewModel viewModel)
        {
            foreach (var child in viewModel.Children)
            {
                UpdateRecursive(child);
            }

            if (viewModel.Id.HasValue && accounts.TryGetValue(viewModel.Id.Value, out var account))
            {
                viewModel.Sum = account.IsGroup
                    ? viewModel.Children.Where(c => !c.IsNotSummable).Sum(c => c.Sum)
                    : account.Sum;
            }
        }

        foreach (var child in Total.Children)
        {
            UpdateRecursive(child);
        }

        Total.RecalcByChildren();
    }

    [RelayCommand]
    public async Task AddGroupAsync()
    {
        var account = new AccountViewModel { IsGroup = true };
        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<AccountViewModel>(account)); ;
        if (result != null)
        {
            Total.Children.Add(result);
            result.Parent = Total;
            result.Id = Guid.NewGuid();
            Save(null, result);
        }
    }

    [RelayCommand]
    public async Task AddElementAsync()
    {
        var account = new AccountViewModel();
        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<AccountViewModel>(account));
        if (result != null)
        {
            var parent = SelectedAccount?.IsGroup == true ? SelectedAccount : (SelectedAccount?.Parent ?? Total);
            parent.Children.Add(result);
            result.Id = Guid.NewGuid();
            result.Parent = parent;
            Save(null, result);
        }
    }

    [RelayCommand]
    public async Task EditAsync(AccountViewModel editAccount)
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
            ImageData = editAccount.ImageData,
            IsNotSummable = editAccount.IsNotSummable,
        };
        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<AccountViewModel>(account));
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

        OnPropertyChanged(nameof(SelectedAccount));
    }

    private void Save(AccountViewModel? one, AccountViewModel other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));
        ArgumentNullException.ThrowIfNull(other.Id, $"{nameof(other)}.{nameof(other.Id)}");

        if (!ImageDataHelper.AreEqual(one?.ImageData, other.ImageData))
        {
            using var stream = new MemoryStream(other.ImageData!);
            _repository!.UpdateImage(other.Id.Value, other.Name, stream);
        }

        if (one != null)
        {
            one.Name = other.Name;
            one.ImageData = other.ImageData;
            one.IsNotSummable = other.IsNotSummable;
        }

        var state = _stateManager.GetMainState();
        _stateManager.SetMainState(state with { Accounts = [.. Total.Children] });

        var existingAccount = _repository!.GetAccount(other.Id.Value);
        _repository!.UpdateAccount(new Models.Account
        {
            Id = other.Id.Value,
            ParentId = other.Parent?.Id,
            Name = other.Name,
            IsGroup = other.IsGroup,
            IsNotSummable = other.IsNotSummable,
            Sum = other.IsGroup ? 0 : existingAccount.Sum,
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
