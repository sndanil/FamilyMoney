using Avalonia.Media;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using FamilyMoney.Utils;
using ReactiveUI;
using System;
namespace FamilyMoney.ViewModels;

public abstract class BaseSubCategoryViewModel : ViewModelBase
{
    private Guid _id;
    private Guid? _categoryId;
    private string _name = string.Empty;

    public Guid Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public Guid? CategoryId
    {
        get => _categoryId;
        set => this.RaiseAndSetIfChanged(ref _categoryId, value);
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public void FillFrom(Guid id, IRepository repository)
    {
        var subCategory = repository.GetSubCategory(id);
        FillFrom(subCategory, repository);
    }

    public void FillFrom(SubCategory subCategory, IRepository repository)
    {
        Id = subCategory.Id;
        Name = subCategory.Name;
        CategoryId = subCategory.CategoryId;
    }
}

public sealed class DebetSubCategoryViewModel : BaseSubCategoryViewModel
{

}

public sealed class CreditSubCategoryViewModel : BaseSubCategoryViewModel
{

}

public sealed class TransferSubCategoryViewModel : BaseSubCategoryViewModel
{

}

