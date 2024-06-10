using Avalonia.Media;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using FamilyMoney.Utils;
using ReactiveUI;
using System;
using System.Collections.Generic;

namespace FamilyMoney.ViewModels;

public abstract class BaseSubCategoryViewModel : ViewModelBase
{
    private Guid _id;
    private Guid? _categoryId;
    private BaseCategoryViewModel? _category;
    private string _name = string.Empty;
    private decimal _lastSum = 0;
    private IList<string> _commments = [];

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

    public BaseCategoryViewModel? Category
    {
        get => _category;
        set => this.RaiseAndSetIfChanged(ref _category, value);
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public decimal LastSum
    {
        get => _lastSum;
        set => this.RaiseAndSetIfChanged(ref _lastSum, value);
    }

    public IList<string> Comments
    {
        get => _commments;
        set => this.RaiseAndSetIfChanged(ref _commments, value);
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

    public override string ToString()
    {
        return Name;
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

