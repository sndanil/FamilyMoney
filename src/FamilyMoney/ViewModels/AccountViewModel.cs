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
    private bool _isHidden = false;
    private bool _isExpanded = true;
    private bool _isNotSummable = false;
    private readonly ObservableCollection<AccountViewModel> _children = [];

    public ICommand SelectCommand { get; }

    public ICommand ToggleExpandCommand { get; }

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

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public bool IsHidden
    {
        get => _isHidden;
        set => this.RaiseAndSetIfChanged(ref _isHidden, value);
    }

    public bool IsNotSummable
    {
        get => _isNotSummable;
        set => this.RaiseAndSetIfChanged(ref _isNotSummable, value);
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

    public ObservableCollection<AccountViewModel> Children { get => _children; }

    public AccountViewModel()
    {
        SelectCommand = ReactiveCommand.CreateFromTask(() =>
        {
            MessageBus.Current.SendMessage(new AccountSelectMessage(Id));
            return Task.CompletedTask;
        });

        ToggleExpandCommand = ReactiveCommand.CreateFromTask(() =>
        {
            this.IsExpanded = !this.IsExpanded;
            MessageBus.Current.SendMessage(new AccountExpandMessage(Id, IsExpanded));
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
        Image = ImageConverter.ToImage(repository.TryGetImage(account.Id));
    }
}

