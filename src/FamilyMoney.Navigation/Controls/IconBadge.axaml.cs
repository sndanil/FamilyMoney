using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FamilyMoney.Navigation.Services;
using FamilyMoney.Utils;
using Material.Icons;
using Material.Icons.Avalonia;

namespace FamilyMoney.Navigation.Controls;

public partial class IconBadge : UserControl
{
    private static readonly ConditionalWeakTable<byte[], Bitmap> ImageCache = [];
    private static readonly Dictionary<string, Bitmap?> PackCache = new(StringComparer.OrdinalIgnoreCase);

    public static readonly StyledProperty<byte[]?> ImageDataProperty =
        AvaloniaProperty.Register<IconBadge, byte[]?>(nameof(ImageData));

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<IconBadge, string?>(nameof(Icon));

    public static readonly StyledProperty<string?> IconColorProperty =
        AvaloniaProperty.Register<IconBadge, string?>(nameof(IconColor));

    public static readonly StyledProperty<string?> EntityNameProperty =
        AvaloniaProperty.Register<IconBadge, string?>(nameof(EntityName));

    public static readonly StyledProperty<MaterialIconKind> FallbackProperty =
        AvaloniaProperty.Register<IconBadge, MaterialIconKind>(nameof(Fallback), MaterialIconKind.TagOutline);

    public static readonly StyledProperty<double> BadgeSizeProperty =
        AvaloniaProperty.Register<IconBadge, double>(nameof(BadgeSize), 36d);

    public static readonly StyledProperty<bool> ShowFallbackProperty =
        AvaloniaProperty.Register<IconBadge, bool>(nameof(ShowFallback), true);

    public static readonly StyledProperty<bool> CollapseWhenEmptyProperty =
        AvaloniaProperty.Register<IconBadge, bool>(nameof(CollapseWhenEmpty), false);

    public IconBadge()
    {
        InitializeComponent();
        UpdateVisual();
    }

    public byte[]? ImageData
    {
        get => GetValue(ImageDataProperty);
        set => SetValue(ImageDataProperty, value);
    }

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? IconColor
    {
        get => GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public string? EntityName
    {
        get => GetValue(EntityNameProperty);
        set => SetValue(EntityNameProperty, value);
    }

    public MaterialIconKind Fallback
    {
        get => GetValue(FallbackProperty);
        set => SetValue(FallbackProperty, value);
    }

    public double BadgeSize
    {
        get => GetValue(BadgeSizeProperty);
        set => SetValue(BadgeSizeProperty, value);
    }

    /// <summary>
    /// Показывать ли подсказку/дефолтную иконку, если у сущности нет
    /// собственной картинки или явно выбранной иконки. Для счетов в списках
    /// выключено: без картинки место остаётся пустым.
    /// </summary>
    public bool ShowFallback
    {
        get => GetValue(ShowFallbackProperty);
        set => SetValue(ShowFallbackProperty, value);
    }

    /// <summary>
    /// Если у сущности нет ни картинки, ни явной иконки — схлопнуть контрол,
    /// чтобы он не оставлял пустое место (например, у групп без картинки).
    /// </summary>
    public bool CollapseWhenEmpty
    {
        get => GetValue(CollapseWhenEmptyProperty);
        set => SetValue(CollapseWhenEmptyProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ImageDataProperty
            || change.Property == IconProperty
            || change.Property == IconColorProperty
            || change.Property == EntityNameProperty
            || change.Property == FallbackProperty
            || change.Property == BadgeSizeProperty
            || change.Property == ShowFallbackProperty
            || change.Property == CollapseWhenEmptyProperty)
        {
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        if (Root is null)
        {
            return;
        }

        var size = double.IsFinite(BadgeSize) && BadgeSize > 0 ? BadgeSize : 36d;
        Width = size;
        Height = size;
        Root.Width = size;
        Root.Height = size;
        var radius = size * 0.28;
        Root.CornerRadius = new CornerRadius(radius);
        Root.Clip = new RectangleGeometry(new Rect(0, 0, size, size))
        {
            RadiusX = radius,
            RadiusY = radius,
        };

        HideAll();

        if (CollapseWhenEmpty && !ShowFallback)
        {
            var hasContent = ImageData is { Length: > 0 } || IconSpec.TryParse(Icon, out _);
            IsVisible = hasContent;
            if (!hasContent)
            {
                return;
            }
        }

        if (ImageData is { Length: > 0 } data)
        {
            var image = DecodeImage(data);
            if (image is not null)
            {
                BaseLayer.IsVisible = true;
                ImageLayer.Source = image;
                ImageLayer.IsVisible = true;
                return;
            }
        }

        var spec = TryResolveSpec();
        if (spec is { Kind: IconSpecKind.Pack } pack)
        {
            var asset = PackCatalog.Instance.Resolve(pack.PackId, pack.AssetId);
            if (asset is not null && TryLoadPack(asset, out var bitmap))
            {
                PackLayer.Source = bitmap;
                PackLayer.Margin = string.Equals(asset.Kind, "card", StringComparison.OrdinalIgnoreCase)
                    ? default
                    : new Thickness(size * 0.08);
                PackLayer.IsVisible = true;
                ColorLayer.Background = new SolidColorBrush(ParseColor(asset.Background, Colors.White));
                ColorLayer.IsVisible = true;
                return;
            }

            if (ShowFallback)
            {
                ApplyMaterial(Fallback, size);
            }

            return;
        }

        if (spec is { Kind: IconSpecKind.Letter } letter)
        {
            ApplyLetter(letter.Letter, size);
            return;
        }

        if (spec is { Kind: IconSpecKind.Material } material)
        {
            ApplyMaterial(material.Material, size);
            return;
        }

        if (ShowFallback)
        {
            ApplyMaterial(Fallback, size);
        }
    }

    private IconSpec? TryResolveSpec()
    {
        if (IconSpec.TryParse(Icon, out var explicitSpec))
        {
            return explicitSpec;
        }

        if (!ShowFallback)
        {
            return null;
        }

        var suggestion = IconSuggester.Suggest(EntityName);
        return IconSpec.TryParse(suggestion, out var suggested) ? suggested : null;
    }

    private void ApplyMaterial(MaterialIconKind kind, double size)
    {
        GlyphLayer.Kind = kind;
        GlyphLayer.Width = size * 0.56;
        GlyphLayer.Height = size * 0.56;
        GlyphLayer.IsVisible = true;
        ColorLayer.Background = new SolidColorBrush(ParseColor(IconColor, Color.Parse(IconColorHelper.Accent)));
        ColorLayer.IsVisible = true;
    }

    private void ApplyLetter(string letter, double size)
    {
        LetterLayer.Text = letter;
        LetterLayer.FontSize = letter.Length > 1 ? size * 0.38 : size * 0.52;
        LetterLayer.IsVisible = true;
        ColorLayer.Background = new SolidColorBrush(ParseColor(IconColor, Color.Parse(IconColorHelper.Accent)));
        ColorLayer.IsVisible = true;
    }

    private void HideAll()
    {
        BaseLayer.IsVisible = false;
        ImageLayer.IsVisible = false;
        PackLayer.IsVisible = false;
        GlyphLayer.IsVisible = false;
        LetterLayer.IsVisible = false;
        ColorLayer.IsVisible = false;
    }

    private static Bitmap? DecodeImage(byte[] data)
    {
        try
        {
            return ImageCache.GetValue(data, static d =>
            {
                using var stream = new MemoryStream(d);
                return Bitmap.DecodeToWidth(stream, 160);
            });
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryLoadPack(PackAssetView asset, out Bitmap? bitmap)
    {
        var key = asset.Uri.ToString();
        if (PackCache.TryGetValue(key, out bitmap))
        {
            return bitmap is not null;
        }

        try
        {
            using var stream = AssetLoader.Open(asset.Uri);
            bitmap = Bitmap.DecodeToWidth(stream, 192);
            PackCache[key] = bitmap;
            return true;
        }
        catch (Exception)
        {
            PackCache[key] = null;
            bitmap = null;
            return false;
        }
    }

    private static Color ParseColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return Color.Parse(value);
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}

internal static class IconColorHelper
{
    public const string Accent = IconColor.Accent;
}
