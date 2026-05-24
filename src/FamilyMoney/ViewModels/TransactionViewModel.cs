using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public partial class BaseTransactionViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial Guid Id { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial AccountViewModel? Account { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial AccountViewModel? ToAccount { get; set; }

    [ObservableProperty]
    public partial bool IsTransfer { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial decimal Sum { get; set; }

    [ObservableProperty]
    public partial decimal ToSum { get; set; }

    [ObservableProperty]
    public partial IList<AccountViewModel>? FlatAccounts { get; set; }

    [ObservableProperty]
    public partial IList<BaseCategoryViewModel>? Categories { get; set; }

    [ObservableProperty]
    public partial IList<BaseSubCategoryViewModel>? SubCategories { get; set; }

    [ObservableProperty]
    public partial BaseCategoryViewModel? Category { get; set; }

    [ObservableProperty]
    public partial string? Comment { get; set; }

    [ObservableProperty]
    public partial IList<string> Comments { get; set; } = [];

    [ObservableProperty]
    public partial string? SubCategoryText { get; set; }

    [ObservableProperty]
    public partial BaseSubCategoryViewModel? SubCategory { get; set; }

    [ObservableProperty]
    public partial DateTime? Date { get; set; }

    [ObservableProperty]
    public partial DateTime? LastChange { get; set; }

    public ObservableCollection<string> Tags { get; } = [];

    [ObservableProperty]
    public partial string TagInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IList<string> SuggestedTags { get; private set; } = [];

    partial void OnSuggestedTagsChanged(IList<string> value)
    {
        NotifySuggestedTagsChanged();
    }

    public IList<string> SelectableSuggestedTags => SuggestedTags
        .Where(t => !Tags.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
        .ToList();

    public bool HasSelectableSuggestedTags => SelectableSuggestedTags.Count > 0;

    public BaseTransactionViewModel()
    {
        Tags.CollectionChanged += (_, _) => NotifySuggestedTagsChanged();
    }

    private bool CanOkCommand()
    {
        return Sum != 0 && Account != null && (!IsTransfer || ToAccount != null);
    }

    [RelayCommand(CanExecute = nameof(CanOkCommand))]
    public async Task OkAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<BaseTransactionViewModel>(this));
    }

    [RelayCommand]
    public async Task CancelAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<BaseTransactionViewModel>(null));
    }

    [RelayCommand]
    public async Task CommentAsync(string comment)
    {
        this.Comment += comment;
    }

    [RelayCommand]
    public async Task PrevDayAsync()
    {
        Date = Date!.Value.AddDays(-1);
    }

    [RelayCommand]
    public async Task NextDayAsync()
    {
        Date = Date!.Value.AddDays(1);
    }

    [RelayCommand]
    public async Task AddTagAsync()
    {
        var tag = TagInput.Trim();
        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        if (!Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
        {
            Tags.Add(tag);
        }

        TagInput = string.Empty;
        NotifySuggestedTagsChanged();
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task SelectTagAsync(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        var trimmed = tag.Trim();
        if (!Tags.Any(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            Tags.Add(trimmed);
        }

        TagInput = string.Empty;
        NotifySuggestedTagsChanged();
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task RemoveTagAsync(string? tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        var existing = Tags.FirstOrDefault(t => string.Equals(t, tag, StringComparison.Ordinal));
        if (existing != null)
        {
            Tags.Remove(existing);
        }

        NotifySuggestedTagsChanged();
        await Task.CompletedTask;
    }

    public void RefreshSuggestedTags()
    {
        if (SubCategory != null
            && Category != null
            && SubCategory.CategoryId == Category.Id)
        {
            SuggestedTags = SubCategory.Tags;
        }
        else
        {
            SuggestedTags = [];
        }

        NotifySuggestedTagsChanged();
    }

    private void NotifySuggestedTagsChanged()
    {
        OnPropertyChanged(nameof(SelectableSuggestedTags));
        OnPropertyChanged(nameof(HasSelectableSuggestedTags));
    }

    public string[]? GetTagsForSave()
    {
        return Tags.Count > 0 ? Tags.ToArray() : null;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(Sum) && IsTransfer)
        {
            ToSum = Sum;
        }
    }

    public virtual void FillFrom(Transaction transaction, IRepository repository)
    {
        Id = transaction.Id;
        Sum = transaction.Sum;
        Date = transaction.Date;
        LastChange = transaction.LastChange;
        Comment = transaction.Comment;

        Tags.Clear();
        if (transaction.Tags != null)
        {
            foreach (var tag in transaction.Tags.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                Tags.Add(tag.Trim());
            }
        }
    }
}

public class DebetTransactionViewModel : BaseTransactionViewModel
{
}

public class CreditTransactionViewModel : BaseTransactionViewModel
{
}

public class TransferTransactionViewModel : DebetTransactionViewModel
{
}
