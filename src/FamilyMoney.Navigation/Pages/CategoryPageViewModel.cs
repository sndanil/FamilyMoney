using System;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FamilyMoney.ViewModels;
using FamilyMoney.ViewModels.Settings;

namespace FamilyMoney.Navigation.Pages;

public enum TransactionsSection
{
    Debet,
    Credit,
    Transfer,
}

/// <summary>
/// Страница категории: подкатегории в виде аккордеона и операции внутри них.
/// </summary>
public partial class CategoryPageViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly TransactionsSection _section;
    private readonly Guid _categoryId;

    private bool _defaultsApplied;

    [ObservableProperty]
    public partial CategoryTransactionsGroupViewModel? Category { get; set; }

    public CategoryPageViewModel(MainViewModel main, CategoryTransactionsGroupViewModel category, TransactionsSection section)
    {
        _main = main;
        _section = section;
        _categoryId = category.Category?.Id ?? Guid.Empty;
        Category = category;

        Title = category.Category?.Name ?? "Без категории";
        SectionGenitive = section switch
        {
            TransactionsSection.Debet => "доходов",
            TransactionsSection.Credit => "расходов",
            _ => "переводов",
        };
    }

    public string Title { get; }

    public string SectionGenitive { get; }

    public TransactionsViewModel Transactions => _main.Transactions;

    public SettingsViewModel Settings => _main.Settings;

    /// <summary>Страница показана: подписываемся на обновления и включаем предзаполнение категории.</summary>
    public void Attach()
    {
        Transactions.PropertyChanged += OnTransactionsChanged;
        ResolveCategory();
        Transactions.AddCategoryContext = Category;
        ApplyDefaultExpansion();
    }

    /// <summary>Страница скрыта или закрыта: снимаем подписку и предзаполнение.</summary>
    public void Detach()
    {
        Transactions.PropertyChanged -= OnTransactionsChanged;

        // Группа могла быть пересобрана после сохранения транзакции, поэтому
        // сравниваем по идентификатору категории, а не по ссылке.
        if (Transactions.AddCategoryContext?.Category?.Id == _categoryId)
        {
            Transactions.AddCategoryContext = null;
        }
    }

    private void OnTransactionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == SectionPropertyName)
        {
            ResolveCategory();
        }
    }

    private void ResolveCategory()
    {
        var group = Section.Categories.FirstOrDefault(c => c.Category?.Id == _categoryId);
        if (group != null && !ReferenceEquals(group, Category))
        {
            Category = group;
        }
    }

    private void ApplyDefaultExpansion()
    {
        if (_defaultsApplied || Category == null)
        {
            return;
        }

        _defaultsApplied = true;

        // Единственный раздел раскрываем сразу, иначе страница выглядит пустой.
        if (Category.SubCategories.Count == 1)
        {
            Transactions.SetExpanded(Category.SubCategories[0], true);
        }
    }

    private SummaryTransactionsGroup Section => _section switch
    {
        TransactionsSection.Debet => _main.Transactions.DebetTransactions,
        TransactionsSection.Credit => _main.Transactions.CreditTransactions,
        _ => _main.Transactions.TransferTransactions,
    };

    private string SectionPropertyName => _section switch
    {
        TransactionsSection.Debet => nameof(TransactionsViewModel.DebetTransactions),
        TransactionsSection.Credit => nameof(TransactionsViewModel.CreditTransactions),
        _ => nameof(TransactionsViewModel.TransferTransactions),
    };
}
