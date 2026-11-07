using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public enum CategoryKind
{
    Debet,
    Credit,
    Transfer,
}

public partial class CategoriesViewModel : ViewModelBase
{
    private readonly IRepository _repository;

    private ObservableCollection<DebetCategoryViewModel> _debetCategories = [];
    private ObservableCollection<CreditCategoryViewModel> _creditCategories = [];
    private ObservableCollection<TransferCategoryViewModel> _transferCategories = [];

    public ObservableCollection<DebetCategoryViewModel> DebetCategories { get => _debetCategories; set => _debetCategories = value; }
    public ObservableCollection<CreditCategoryViewModel> CreditCategories { get => _creditCategories; set => _creditCategories = value; }
    public ObservableCollection<TransferCategoryViewModel> TransferCategories { get => _transferCategories; set => _transferCategories = value; }

    /// <summary>Категории выбранного типа с учётом поиска и показа скрытых.</summary>
    public ObservableCollection<BaseCategoryViewModel> VisibleCategories { get; } = [];

    [ObservableProperty]
    public partial CategoryKind SelectedType { get; set; } = CategoryKind.Debet;

    [ObservableProperty]
    public partial BaseCategoryViewModel? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial string? RenameError { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowHidden { get; set; }

    public bool IsDebetSelected => SelectedType == CategoryKind.Debet;

    public bool IsCreditSelected => SelectedType == CategoryKind.Credit;

    public bool IsTransferSelected => SelectedType == CategoryKind.Transfer;

    public string SelectedTypeTitle => SelectedType switch
    {
        CategoryKind.Debet => "Доход",
        CategoryKind.Credit => "Расход",
        CategoryKind.Transfer => "Перевод",
        _ => string.Empty,
    };

    public bool HasVisibleCategories => VisibleCategories.Count > 0;

    public bool HasNoVisibleCategories => !HasVisibleCategories;

    private bool _isLoaded;

    public CategoriesViewModel(IRepository repository)
    {
        _repository = repository;
    }

    public void Invalidate()
    {
        _isLoaded = false;
    }

    public void EnsureLoaded()
    {
        if (_isLoaded)
        {
            return;
        }

        Reload();
        _isLoaded = true;
    }

    public void Reload()
    {
        DebetCategories.Clear();
        CreditCategories.Clear();
        TransferCategories.Clear();

        var categories = _repository.GetCategories().ToList();

        DebetCategories.AddRange(GetByTypes<DebetCategory, DebetCategoryViewModel>(categories));
        CreditCategories.AddRange(GetByTypes<CreditCategory, CreditCategoryViewModel>(categories));
        TransferCategories.AddRange(GetByTypes<TransferCategory, TransferCategoryViewModel>(categories));

        LoadSubCategories(categories);
        RefreshVisibleCategories();
    }

    partial void OnSelectedCategoryChanged(BaseCategoryViewModel? value)
    {
        RenameError = null;
    }

    partial void OnSelectedTypeChanged(CategoryKind value)
    {
        OnPropertyChanged(nameof(IsDebetSelected));
        OnPropertyChanged(nameof(IsCreditSelected));
        OnPropertyChanged(nameof(IsTransferSelected));
        OnPropertyChanged(nameof(SelectedTypeTitle));
        RefreshVisibleCategories();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshVisibleCategories();
    }

    partial void OnShowHiddenChanged(bool value)
    {
        RefreshVisibleCategories();
    }

    private void RefreshVisibleCategories()
    {
        IEnumerable<BaseCategoryViewModel> source = SelectedType switch
        {
            CategoryKind.Debet => DebetCategories,
            CategoryKind.Credit => CreditCategories,
            CategoryKind.Transfer => TransferCategories,
            _ => [],
        };

        var search = SearchText.Trim();
        var filtered = source
            .Where(c => ShowHidden || !c.IsHidden)
            .Where(c => search.Length == 0 || c.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var previous = SelectedCategory;
        VisibleCategories.Clear();

        foreach (var category in filtered)
        {
            VisibleCategories.Add(category);
        }

        OnPropertyChanged(nameof(HasVisibleCategories));
        OnPropertyChanged(nameof(HasNoVisibleCategories));

        if (previous != null && VisibleCategories.Contains(previous))
        {
            SelectedCategory = previous;
        }
        else
        {
            SelectedCategory = VisibleCategories.FirstOrDefault();
        }
    }

    private void LoadSubCategories(IEnumerable<Category> categories)
    {
        var allSubCategories = _repository.GetSubCategories().ToList();
        var from = DateTime.Today.AddYears(-1);
        var lastSums = _repository.GetLastSumsBySubCategories(from, allSubCategories.Select(s => s.Id))
            .ToDictionary(s => s.SubCategoryId);

        var byCategory = allSubCategories
            .Where(s => s.CategoryId != null)
            .GroupBy(s => s.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Name).ToList());

        foreach (var category in AllCategories())
        {
            category.SubCategories.Clear();

            if (!byCategory.TryGetValue(category.Id, out var subCategories))
            {
                continue;
            }

            foreach (var subCategory in subCategories)
            {
                var viewModel = new BaseSubCategoryViewModel
                {
                    Id = subCategory.Id,
                    CategoryId = subCategory.CategoryId,
                    Name = subCategory.Name,
                    Category = category,
                    LastSum = lastSums.TryGetValue(subCategory.Id, out var sum) ? sum.Sum : 0m,
                };

                category.SubCategories.Add(viewModel);
            }
        }
    }

    private IEnumerable<BaseCategoryViewModel> AllCategories()
    {
        return DebetCategories.Cast<BaseCategoryViewModel>()
            .Concat(CreditCategories)
            .Concat(TransferCategories);
    }

    [RelayCommand]
    public async Task SelectTypeAsync(CategoryKind type)
    {
        SelectedType = type;
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task CreateCategoryAsync()
    {
        switch (SelectedType)
        {
            case CategoryKind.Debet:
                await CreateCategory(DebetCategories);
                break;
            case CategoryKind.Credit:
                await CreateCategory(CreditCategories);
                break;
            case CategoryKind.Transfer:
                await CreateCategory(TransferCategories);
                break;
        }
    }

    [RelayCommand]
    public async Task CreateDebetAsync()
    {
        await CreateCategory(DebetCategories);
    }

    [RelayCommand]
    public async Task CreateCreditAsync()
    {
        await CreateCategory(CreditCategories);
    }

    [RelayCommand]
    public async Task CreateTransferAsync()
    {
        await CreateCategory(TransferCategories);
    }

    private async Task CreateCategory<C>(ObservableCollection<C> categories) where C : BaseCategoryViewModel, new()
    {
        var category = new C
        {
            Id = Guid.NewGuid()
        };

        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<BaseCategoryViewModel>(category));
        if (result != null)
        {
            SaveCategory(result, null);
            categories.Add((C)result);
            RefreshVisibleCategories();
            SelectedCategory = result;
        }
    }

    [RelayCommand]
    public async Task EditAsync(BaseCategoryViewModel? editCategory)
    {
        if (editCategory == null)
        {
            return;
        }

        BaseCategoryViewModel category = new DebetCategoryViewModel();
        if (editCategory is CreditCategoryViewModel)
        {
            category = new CreditCategoryViewModel();
        }
        else if (editCategory is TransferCategoryViewModel)
        {
            category = new TransferCategoryViewModel();
        }

        category.Id = editCategory.Id;
        category.Name = editCategory.Name;
        category.ImageData = editCategory.ImageData;
        category.IsHidden = editCategory.IsHidden;

        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<BaseCategoryViewModel>(category));
        if (result != null)
        {
            SaveCategory(result, editCategory);
            RefreshVisibleCategories();
        }
    }

    [RelayCommand]
    public async Task ToggleHiddenAsync(BaseCategoryViewModel? category)
    {
        if (category == null)
        {
            return;
        }

        category.IsHidden = !category.IsHidden;
        SaveCategory(category, category);
        RefreshVisibleCategories();
        await Task.CompletedTask;
    }

    [RelayCommand]
    public void BeginRenameSubCategory(BaseSubCategoryViewModel? subCategory)
    {
        if (subCategory == null || SelectedCategory == null)
        {
            return;
        }

        foreach (var item in SelectedCategory.SubCategories)
        {
            item.IsEditing = false;
        }

        RenameError = null;
        subCategory.EditName = subCategory.Name;
        subCategory.IsEditing = true;
    }

    [RelayCommand]
    public void CancelRenameSubCategory(BaseSubCategoryViewModel? subCategory)
    {
        if (subCategory == null)
        {
            return;
        }

        subCategory.IsEditing = false;
        RenameError = null;
    }

    [RelayCommand]
    public void SaveRenameSubCategory(BaseSubCategoryViewModel? subCategory)
    {
        if (subCategory == null)
        {
            return;
        }

        var name = (subCategory.EditName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            RenameError = "Название не может быть пустым.";
            return;
        }

        if (SelectedCategory != null
            && SelectedCategory.SubCategories.Any(s =>
                s.Id != subCategory.Id
                && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            RenameError = "Такая подкатегория уже есть в этой категории.";
            return;
        }

        var entity = _repository.GetSubCategory(subCategory.Id);
        if (entity != null)
        {
            entity.Name = name;
            _repository.UpdateSubCategory(entity);
        }

        subCategory.Name = name;
        subCategory.IsEditing = false;
        RenameError = null;
        WeakReferenceMessenger.Default.Send(new SubCategoryRenamedMessage(subCategory.Id, name));
    }

    private void SaveCategory(BaseCategoryViewModel categoryForSave, BaseCategoryViewModel? categoryForUpdate)
    {
        Func<Category> factory = () => new DebetCategory
        {
            Id = categoryForSave.Id,
            Name = categoryForSave.Name,
        };

        if (categoryForSave is DebetCategoryViewModel)
        {
        }
        else if (categoryForSave is CreditCategoryViewModel)
        {
            factory = () => new CreditCategory
            {
                Id = categoryForSave.Id,
                Name = categoryForSave.Name,
            };
        }
        else if (categoryForSave is TransferCategoryViewModel)
        {
            factory = () => new TransferCategory
            {
                Id = categoryForSave.Id,
                Name = categoryForSave.Name,
            };
        }

        if (!ImageDataHelper.AreEqual(categoryForUpdate?.ImageData, categoryForSave.ImageData)
            && categoryForSave.ImageData != null)
        {
            using var stream = new MemoryStream(categoryForSave.ImageData);
            _repository!.UpdateImage(categoryForSave.Id, categoryForSave.Name, stream);
        }

        var category = factory();
        category.IsHidden = categoryForSave.IsHidden;
        _repository.UpdateCategroty(category);

        WeakReferenceMessenger.Default.Send(new CategoryUpdateMessage(category.Id));

        if (categoryForUpdate is not null)
        {
            categoryForUpdate.Name = categoryForSave.Name;
            categoryForUpdate.ImageData = categoryForSave.ImageData;
            categoryForUpdate.IsHidden = categoryForSave.IsHidden;
        }
    }

    private IEnumerable<T> GetByTypes<C, T>(IEnumerable<Category> categories) where C : Category where T : BaseCategoryViewModel, new()
    {
        return categories.OfType<C>().Select(c =>
        {
            var category = new T();
            category.FillFrom(c, _repository);
            return category;
        });
    }
}
