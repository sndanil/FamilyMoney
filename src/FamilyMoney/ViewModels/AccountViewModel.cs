using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.State;
using FamilyMoney.Utils;
using System.Collections.ObjectModel;

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

    /// <summary>Последний видимый счёт в группе — у него не рисуется нижний разделитель.</summary>
    [ObservableProperty]
    public partial bool IsLastVisibleChild { get; set; }

    [ObservableProperty]
    public partial DateTime? EndsOn { get; set; }

    [ObservableProperty]
    public partial decimal? AnnualRate { get; set; }

    [ObservableProperty]
    public partial string AnnualRateText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial string? AnnualRateError { get; set; }

    private int _depositWarningDays = RootConfiguration.DefaultDepositWarningDays;

    public bool IsAloneGroup => Children.Count == 0;

    /// <summary>
    /// Уровень вложенности: 0 — корневой счёт или группа, 1 — вложенный счёт.
    /// Служебный корень «Всего» не учитывается.
    /// </summary>
    public int Level => Parent?.Parent is null ? 0 : Parent.Level + 1;

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

    [RelayCommand]
    public void ToggleHideCommand()
    {
        IsHidden = !IsHidden;
        WeakReferenceMessenger.Default.Send(new AccountHideMessage(Id, IsHidden));
    }

    public string? DepositCaption => CurrentDepositCaption.Text;

    public string? DepositCaptionCompact => CurrentCompactDepositCaption.Text;

    public bool HasDepositDetails => !IsGroup && CurrentDepositCaption.HasText;

    public bool IsDepositEndingSoon => !IsGroup && CurrentDepositCaption.Kind == DepositTermKind.Soon;

    public bool IsDepositExpired => !IsGroup && CurrentDepositCaption.Kind == DepositTermKind.Expired;

    private DepositCaption CurrentDepositCaption =>
        DepositCaptionFormatter.Format(EndsOn, AnnualRate, _depositWarningDays, DateTime.Today);

    private DepositCaption CurrentCompactDepositCaption =>
        DepositCaptionFormatter.FormatCompact(EndsOn, AnnualRate, _depositWarningDays, DateTime.Today);

    public void SetDepositWarningDays(int days)
    {
        var normalized = Math.Clamp(days, 0, RootConfiguration.MaxDepositWarningDays);
        if (_depositWarningDays == normalized)
        {
            return;
        }

        _depositWarningDays = normalized;
        NotifyDepositDetails();
    }

    private bool CanOkCommand() => !string.IsNullOrEmpty(Name) && (IsGroup || string.IsNullOrEmpty(AnnualRateError));

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

    public void AddFromAccount(IRepository repository, IAccountLocalSettingsStore localSettings, IEnumerable<Account> accounts)
    {
        var viewModels = accounts.Where(a => a.ParentId == Id).OrderBy(a => a.Order).Select(a =>
        {
            var account = new AccountViewModel();
            account.FillFrom(a, repository, localSettings);
            account.Parent = this;
            return account;
        });

        foreach (var item in viewModels)
        {
            Children.Add(item);
        }

        foreach (var account in Children)
        {
            account.AddFromAccount(repository, localSettings, accounts);
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

    public void FillFrom(Account account, IRepository repository, IAccountLocalSettingsStore localSettings)
    {
        Id = account.Id;
        Name = account.Name;
        Sum = account.Sum;
        IsGroup = account.IsGroup;
        IsHidden = localSettings.IsHidden(account.Id);
        IsExpanded = localSettings.IsExpanded(account.Id);
        IsNotSummable = account.IsNotSummable;
        EndsOn = account.EndsOn?.Date;
        AnnualRateText = DepositCaptionFormatter.FormatRateInput(account.AnnualRate);
        ImageData = ImageDataHelper.ToByteArray(repository.TryGetImage(account.Id));
    }

    partial void OnEndsOnChanged(DateTime? value)
    {
        if (value is { } date && date.TimeOfDay != TimeSpan.Zero)
        {
            EndsOn = date.Date;
            return;
        }

        NotifyDepositDetails();
    }

    partial void OnAnnualRateChanged(decimal? value)
    {
        NotifyDepositDetails();
    }

    partial void OnAnnualRateTextChanged(string value)
    {
        if (DepositCaptionFormatter.TryParseRate(value, out var rate))
        {
            AnnualRateError = null;
            AnnualRate = rate;
        }
        else
        {
            AnnualRateError = "Введите неотрицательное число";
        }
    }

    partial void OnIsGroupChanged(bool value)
    {
        NotifyDepositDetails();
        OkCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearEndsOn()
    {
        EndsOn = null;
    }

    [RelayCommand]
    private void ClearAnnualRate()
    {
        AnnualRateText = string.Empty;
    }

    private void NotifyDepositDetails()
    {
        OnPropertyChanged(nameof(DepositCaption));
        OnPropertyChanged(nameof(DepositCaptionCompact));
        OnPropertyChanged(nameof(HasDepositDetails));
        OnPropertyChanged(nameof(IsDepositEndingSoon));
        OnPropertyChanged(nameof(IsDepositExpired));
    }
}
