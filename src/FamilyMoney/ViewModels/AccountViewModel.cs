using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using DynamicData;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.Utils;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class AccountViewModel : ViewModelBase
{
    private decimal _sum = 0;
    private Guid? _id = null;
    private AccountViewModel? _parent = null;
    private string _name = string.Empty;
    private IImage? _image = null;
    private bool _isSelected = false;
    private bool _isGroup = false;
    private ObservableCollection<AccountViewModel> _children = new();

    public ICommand SelectCommand { get; }

    public ReactiveCommand<Unit, AccountViewModel?> OkCommand { get; }

    public ReactiveCommand<Unit, AccountViewModel?> CancelCommand { get; }

    public static Interaction<AccountViewModel, AccountViewModel?> ShowDialog { get; } = new();

    public decimal Sum
    {
        get => _sum;
        set => this.RaiseAndSetIfChanged(ref _sum, value);
    }

    public Guid? Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public AccountViewModel? Parent
    {
        get => _parent;
        set => this.RaiseAndSetIfChanged(ref _parent, value);
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public IImage? Image
    {
        get => _image;
        set => this.RaiseAndSetIfChanged(ref _image, value);
    }

    public bool IsGroup
    {
        get => _isGroup;
        set => this.RaiseAndSetIfChanged(ref _isGroup, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public bool IsAloneGroup
    {
        get => Children.Count == 0;
    }

    public bool IsLastElement
    {
        get => Parent?.Children.IndexOf(this) == Parent?.Children.Count - 1;
    }

    public ObservableCollection<AccountViewModel> Children { get => _children; }

    public AccountViewModel()
    {
        SelectCommand = ReactiveCommand.CreateFromTask(() =>
        {
            MessageBus.Current.SendMessage(new AccountSelectMessage { AccountId = Id });
            return Task.CompletedTask;
        });

        var canExecute = this.WhenAnyValue(x => x.Name, (name) => !string.IsNullOrEmpty(name));
        OkCommand = ReactiveCommand.Create(() =>
        {
            return (AccountViewModel?)this;
        },
        canExecute);

        CancelCommand = ReactiveCommand.Create(() =>
        {
            return (AccountViewModel?)null;
        });
    }

    public void AddFromAccount(IRepository repository, IEnumerable<Models.Account> accounts)
    {
        var viewModels = accounts.Where(a => a.ParentId == Id).OrderBy(a => a.Order).Select(a =>
        {
            var account = new AccountViewModel();
            account.FillFrom(a, repository);
            account.Parent = this;

            return account;
        });

        Children.AddRange(viewModels);

        foreach (var account in Children)
        {
            account.AddFromAccount(repository, accounts);
        }

        if (IsGroup)
        {
            Sum = Children.Sum(c => c.Sum);
        }
    }

    public void FillFrom(Account account, IRepository repository)
    {
        Id = account.Id;
        Name = account.Name;
        Sum = account.Sum;
        IsGroup = account.IsGroup;
        Image = ImageConverter.ToImage(repository.TryGetImage(account.Id));
    }
}

