using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.Utils;
using System;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public abstract partial class BaseCategoryViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial Guid Id { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsHidden { get; set; }

    [ObservableProperty]
    public partial byte[]? ImageData { get; set; }

    private bool CanOkCommand() => !string.IsNullOrEmpty(Name);

    [RelayCommand(CanExecute = nameof(CanOkCommand))]
    public async Task OkAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<BaseCategoryViewModel>(this));
    }

    [RelayCommand]
    public async Task CancelAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<BaseCategoryViewModel>(null));
    }

    public void FillFrom(Guid id, IRepository repository)
    {
        var category = repository.GetCategory(id);
        if (category != null)
        {
            FillFrom(category, repository);
        }
    }

    public void FillFrom(Category category, IRepository repository)
    {
        Id = category.Id;
        Name = category.Name;
        IsHidden = category.IsHidden;
        ImageData = ImageDataHelper.ToByteArray(repository.TryGetImage(Id));
    }
}

public sealed class DebetCategoryViewModel : BaseCategoryViewModel;

public sealed class CreditCategoryViewModel : BaseCategoryViewModel;

public sealed class TransferCategoryViewModel : BaseCategoryViewModel;
