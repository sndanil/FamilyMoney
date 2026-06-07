using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public partial class AccountViewModel : ViewModelBase
{
    private readonly ObservableCollection<AccountViewModel> _children = [];

    [ObservableProperty]
    public partial decimal Sum { get; set; }

    [ObservableProperty]
    public partial Guid? Id { get; set; }

    [ObservableProperty]
    public partial AccountViewModel? Parent { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial byte[]? ImageData { get; set; }

    [ObservableProperty]
    public partial bool IsGroup { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsHidden { get; set; }

    [ObservableProperty]
    public partial bool IsNotSummable { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public bool IsAloneGroup => Children.Count == 0;

    public ObservableCollection<AccountViewModel> Children => _children;

    [RelayCommand]
    public void SelectCommand()
    {
        WeakReferenceMessenger.Default.Send(new AccountSelectMessage(Id));
    }

    [RelayCommand]
    public void ToggleExpandCommand()
    {
        IsExpanded = !IsExpanded;
        WeakReferenceMessenger.Default.Send(new AccountExpandMessage(Id, IsExpanded));
    }

    private bool CanOkCommand() => !string.IsNullOrEmpty(Name);

    [RelayCommand(CanExecute = nameof(CanOkCommand))]
    public async Task OkAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<AccountViewModel>(this));
    }

    [RelayCommand]
    public async Task CancelAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<AccountViewModel>(null));
    }

    public void AddFromAccount(IRepository repository, IEnumerable<Account> accounts)
    {
        var viewModels = accounts.Where(a => a.ParentId == Id).OrderBy(a => a.Order).Select(a =>
        {
            var account = new AccountViewModel();
            account.FillFrom(a, repository);
            account.Parent = this;
            return account;
        });

        foreach (var item in viewModels)
        {
            Children.Add(item);
        }

        foreach (var account in Children)
        {
            account.AddFromAccount(repository, accounts);
        }

        RecalcByChildren();
    }

    public void RecalcByChildren()
    {
        if (IsGroup)
        {
            Sum = Children.Where(a => !a.IsNotSummable).Sum(c => c.Sum);
        }
    }

    public void FillFrom(Account account, IRepository repository)
    {
        Id = account.Id;
        Name = account.Name;
        Sum = account.Sum;
        IsGroup = account.IsGroup;
        IsHidden = account.IsHidden;
        IsExpanded = account.IsExpanded;
        IsNotSummable = account.IsNotSummable;
        ImageData = ImageDataHelper.ToByteArray(repository.TryGetImage(account.Id));
    }
}
