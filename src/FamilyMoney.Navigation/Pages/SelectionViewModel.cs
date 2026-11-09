using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Pages;

/// <summary>Новое имя подкатегории, которого ещё нет в списке.</summary>
public sealed record NewSubCategorySelection(string Name)
{
    public string Caption => "Создать «" + Name + "»";
}

public partial class SelectionViewModel : ObservableObject
{
    private readonly IReadOnlyList<object> _items;
    private readonly bool _allowCreate;

    public SelectionViewModel(
        IEnumerable<object> items,
        object? selectedItem,
        string title,
        string searchPlaceholder,
        string emptyText,
        bool allowCreate = false,
        bool showSubCategoryParent = false)
    {
        _items = items.ToList();
        _allowCreate = allowCreate;
        ShowSubCategoryParent = showSubCategoryParent;
        SelectedItem = selectedItem;
        Title = title;
        SearchPlaceholder = searchPlaceholder;
        EmptyText = emptyText;
        RefreshFilteredItems();
    }

    public bool ShowSubCategoryParent { get; }

    public string Title { get; }

    public string SearchPlaceholder { get; }

    public string EmptyText { get; }

    public ObservableCollection<object> FilteredItems { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial object? SelectedItem { get; set; }

    public bool HasNoItems => FilteredItems.Count == 0;

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilteredItems();
    }

    private void RefreshFilteredItems()
    {
        var search = SearchText.Trim();
        var filtered = search.Length == 0
            ? _items
            : _items.Where(item => GetName(item).Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        var hasExactName = search.Length > 0
            && _items.Any(item => string.Equals(GetName(item), search, StringComparison.OrdinalIgnoreCase));

        FilteredItems.Clear();
        if (_allowCreate && search.Length > 0 && !hasExactName)
        {
            FilteredItems.Add(new NewSubCategorySelection(search));
        }

        foreach (var item in filtered)
        {
            FilteredItems.Add(item);
        }

        OnPropertyChanged(nameof(HasNoItems));
    }

    private static string GetName(object item)
    {
        return item switch
        {
            AccountViewModel account => account.Name,
            BaseCategoryViewModel category => category.Name,
            BaseSubCategoryViewModel subCategory => subCategory.Name,
            string text => text,
            _ => string.Empty,
        };
    }
}
