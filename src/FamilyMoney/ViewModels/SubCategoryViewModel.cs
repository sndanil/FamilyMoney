using CommunityToolkit.Mvvm.ComponentModel;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using System;
using System.Collections.Generic;

namespace FamilyMoney.ViewModels;

public partial class BaseSubCategoryViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial Guid Id { get; set; }

    [ObservableProperty]
    public partial Guid? CategoryId {  get; set; }

    [ObservableProperty]
    public partial BaseCategoryViewModel? Category {  get; set; }

    [ObservableProperty]
    public partial string Name {  get; set; }

    [ObservableProperty]
    public partial decimal LastSum {  get; set; }

    [ObservableProperty]
    public partial IList<string> Comments {  get; set; }

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

