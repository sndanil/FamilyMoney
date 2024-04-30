using Avalonia.Media;
using ReactiveUI;
using System;
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

