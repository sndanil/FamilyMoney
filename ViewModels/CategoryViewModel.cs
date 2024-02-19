using Avalonia.Media;
using ReactiveUI;
namespace FamilyMoney.ViewModels;

public class BaseCategoryViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private IImage? _image = null;

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

public class DebetCategoryViewModel : BaseCategoryViewModel
{

}

public class CreditCategoryViewModel : BaseCategoryViewModel
{

}

