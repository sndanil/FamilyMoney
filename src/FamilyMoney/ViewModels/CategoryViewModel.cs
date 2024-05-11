using Avalonia.Media;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using FamilyMoney.Utils;
using ReactiveUI;
using System;
using System.Reactive;
using System.Windows.Input;
namespace FamilyMoney.ViewModels;

public abstract class BaseCategoryViewModel : ViewModelBase
{
    private Guid _id;
    private string _name = string.Empty;
    private IImage? _image = null;

    public Guid Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
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

    public ReactiveCommand<Unit, BaseCategoryViewModel?> OkCommand { get; }

    public ReactiveCommand<Unit, BaseCategoryViewModel?> CancelCommand { get; }

    public static Interaction<BaseCategoryViewModel, BaseCategoryViewModel?> ShowDialog { get; } = new();

    protected BaseCategoryViewModel()
    {
        var canExecute = this.WhenAnyValue(x => x.Name, (name) => !string.IsNullOrEmpty(name));
        OkCommand = ReactiveCommand.Create(() =>
        {
            return (BaseCategoryViewModel?)this;
        },
        canExecute);

        CancelCommand = ReactiveCommand.Create(() =>
        {
            return (BaseCategoryViewModel?)null;
        });
    }

    public void FillFrom(Guid id, IRepository repository)
    {
        var category = repository.GetCategory(id);
        FillFrom(category, repository);
    }

    public void FillFrom(Category category, IRepository repository)
    {
        Id = category.Id;
        Name = category.Name;
        Image = ImageConverter.ToImage(repository.TryGetImage(Id));
    }
}

public sealed class DebetCategoryViewModel : BaseCategoryViewModel
{

}

public sealed class CreditCategoryViewModel : BaseCategoryViewModel
{

}

public sealed class TransferCategoryViewModel : BaseCategoryViewModel
{

}

