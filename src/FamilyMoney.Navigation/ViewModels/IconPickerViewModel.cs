using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using FamilyMoney.Navigation.Services;
using FamilyMoney.Utils;

namespace FamilyMoney.Navigation.ViewModels;

public sealed record IconSelection(string? Icon, string? IconColor, byte[]? ImageData);

public sealed class IconPickerViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    private string? _selectedIcon;
    private string? _selectedColor;
    private byte[]? _imageData;
    private string _letterText;

    public IconPickerViewModel(
        string? entityName,
        bool isAccount,
        string? icon,
        string? iconColor,
        byte[]? imageData)
    {
        EntityName = entityName ?? string.Empty;
        IsAccount = isAccount;
        _selectedIcon = IconSpec.TryParse(icon, out _) ? icon : null;
        _selectedColor = IconColor.TryParse(iconColor, out var normalizedColor) ? normalizedColor : null;
        if (IconSpec.TryParse(icon, out var current) && current.Kind == IconSpecKind.Pack)
        {
            _selectedColor = null;
        }

        _imageData = imageData;
        _letterText = IconSpec.TryParse(icon, out var spec) && spec.Kind == IconSpecKind.Letter
            ? spec.Letter
            : IconLetter.Suggest(entityName);

        Colors = new ObservableCollection<string>(IconPalette.Colors);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string EntityName { get; }

    public bool IsAccount { get; }

    public ObservableCollection<IconChoice> Icons { get; } = [];

    public ObservableCollection<PackAssetView> ReadyAssets { get; } = [];

    public ObservableCollection<string> Colors { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                Refresh();
            }
        }
    }

    public string? SelectedIcon
    {
        get => _selectedIcon;
        private set
        {
            if (SetField(ref _selectedIcon, value))
            {
                OnPropertyChanged(nameof(PreviewIcon));
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public string? SelectedColor
    {
        get => _selectedColor;
        private set
        {
            if (SetField(ref _selectedColor, value))
            {
                OnPropertyChanged(nameof(PreviewColor));
            }
        }
    }

    public byte[]? ImageData
    {
        get => _imageData;
        private set
        {
            if (SetField(ref _imageData, value))
            {
                OnPropertyChanged(nameof(PreviewImage));
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public string LetterText
    {
        get => _letterText;
        set
        {
            if (SetField(ref _letterText, value))
            {
                SelectedIcon = IconLetter.TryNormalize(value, out var letter)
                    ? IconSpec.ForLetter(letter)
                    : null;
                ImageData = null;
            }
        }
    }

    public string? PreviewIcon => SelectedIcon;

    public string? PreviewColor => SelectedColor;

    public byte[]? PreviewImage => ImageData;

    public bool HasSelection => SelectedIcon != null || ImageData is { Length: > 0 };

    public void SelectIcon(IconChoice choice)
    {
        SelectedIcon = choice.Spec;
        ImageData = null;
    }

    public void SelectPack(PackAssetView asset)
    {
        SelectedIcon = asset.Spec;
        SelectedColor = null;
        ImageData = null;
    }

    public void SelectLetter(string value)
    {
        if (IconLetter.TryNormalize(value, out var letter))
        {
            LetterText = letter;
            SelectedIcon = IconSpec.ForLetter(letter);
        }
        else
        {
            LetterText = value;
            SelectedIcon = null;
        }

        ImageData = null;
    }

    public void SelectImage(byte[]? data)
    {
        ImageData = data is { Length: > 0 } ? data : null;
        if (ImageData != null)
        {
            SelectedIcon = null;
            SelectedColor = null;
        }
    }

    public void SelectColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            SelectedColor = null;
            return;
        }

        if (IconColor.TryParse(color, out var normalized))
        {
            SelectedColor = normalized;
        }
    }

    public void Clear()
    {
        SelectedIcon = null;
        SelectedColor = null;
        ImageData = null;
        _letterText = string.Empty;
        OnPropertyChanged(nameof(LetterText));
    }

    public IconSelection BuildResult()
    {
        if (ImageData is { Length: > 0 })
        {
            return new IconSelection(null, null, ImageData);
        }

        if (SelectedIcon is null)
        {
            return new IconSelection(null, null, null);
        }

        var color = IconSpec.TryParse(SelectedIcon, out var spec) && spec.Kind == IconSpecKind.Pack
            ? null
            : SelectedColor;
        return new IconSelection(SelectedIcon, color, null);
    }

    private void Refresh()
    {
        var query = _searchText.Trim();
        Icons.Clear();
        foreach (var choice in IconCatalog.Search(query))
        {
            Icons.Add(choice);
        }

        ReadyAssets.Clear();
        foreach (var asset in PackCatalog.Instance.Assets)
        {
            if (query.Length == 0 || Matches(asset, query))
            {
                ReadyAssets.Add(asset);
            }
        }

        OnPropertyChanged(nameof(Icons));
        OnPropertyChanged(nameof(ReadyAssets));
    }

    private static bool Matches(PackAssetView asset, string query)
    {
        return asset.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || asset.Spec.Contains(query, StringComparison.OrdinalIgnoreCase)
            || asset.Aliases.Any(alias => alias.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
