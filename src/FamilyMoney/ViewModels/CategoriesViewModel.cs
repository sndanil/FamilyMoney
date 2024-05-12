using Avalonia.Media.Imaging;
using DynamicData;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class CategoriesViewModel : ViewModelBase
{
    private readonly IRepository _repository;

    private ObservableCollection<DebetCategoryViewModel> _debetCategories = [];
    private ObservableCollection<CreditCategoryViewModel> _creditCategories = [];
    private ObservableCollection<TransferCategoryViewModel> _transferCategories = [];

    public ObservableCollection<DebetCategoryViewModel> DebetCategories { get => _debetCategories; set => _debetCategories = value; }
    public ObservableCollection<CreditCategoryViewModel> CreditCategories { get => _creditCategories; set => _creditCategories = value; }
    public ObservableCollection<TransferCategoryViewModel> TransferCategories { get => _transferCategories; set => _transferCategories = value; }

    public ICommand CreateDebetCommand { get; }
    public ICommand CreateCreditCommand { get; }
    public ICommand CreateTransferCommand { get; }
    public ICommand EditCommand { get; }

    public CategoriesViewModel(IRepository repository)
    {
        _repository = repository;

        var categories = _repository.GetCategories();

        DebetCategories.AddRange(GetByTypes<DebetCategory, DebetCategoryViewModel>(categories));
        CreditCategories.AddRange(GetByTypes<CreditCategory, CreditCategoryViewModel>(categories));
        TransferCategories.AddRange(GetByTypes<TransferCategory, TransferCategoryViewModel>(categories));

        EditCommand = ReactiveCommand.CreateFromTask<BaseCategoryViewModel>(EditCategory);
        CreateDebetCommand = ReactiveCommand.CreateFromTask(async () => await CreateCategory(DebetCategories));
        CreateCreditCommand = ReactiveCommand.CreateFromTask(async () => await CreateCategory(CreditCategories));
        CreateTransferCommand = ReactiveCommand.CreateFromTask(async () => await CreateCategory(TransferCategories));
    }

    private async Task CreateCategory<C>(ObservableCollection<C> categories) where C: BaseCategoryViewModel, new()
    {
        var category = new C
        {
            Id = Guid.NewGuid()
        };

        var result = await BaseCategoryViewModel.ShowDialog.Handle(category);
        if (result != null)
        {
            SaveCategory(result, null);
            categories.Add((C)result);
        }
    }

    private async Task EditCategory(BaseCategoryViewModel editCategory)
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
        category.Image = editCategory.Image;
        category.IsHidden = editCategory.IsHidden;

        var result = await BaseCategoryViewModel.ShowDialog.Handle(category);
        if (result != null)
        {
            SaveCategory(result, editCategory);
        }
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

        if (categoryForUpdate?.Image != categoryForSave.Image)
        {
            var stream = new MemoryStream();
            ((Bitmap)categoryForSave.Image!).Save(stream);
            stream.Position = 0;
            _repository!.UpdateImage(categoryForSave.Id, categoryForSave.Name, stream);
        }

        var category = factory();
        category.IsHidden = categoryForSave.IsHidden;
        _repository.UpdateCategroty(category);

        if (categoryForUpdate is not null)
        {
            categoryForUpdate.Name = categoryForSave.Name;
            categoryForUpdate.Image = categoryForSave.Image;
            categoryForUpdate.IsHidden = categoryForSave.IsHidden;
        }
    }

    private IEnumerable<T> GetByTypes<C, T>(IEnumerable<Category> categories)where C : Category where T: BaseCategoryViewModel, new()
    {
        return categories.OfType<C>().Select(c =>
        {
            var category = new T();
            category.FillFrom(c, _repository);
            return category;
        });
    }
}
