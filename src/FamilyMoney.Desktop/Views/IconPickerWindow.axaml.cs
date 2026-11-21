using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FamilyMoney.Navigation.Services;
using FamilyMoney.Navigation.ViewModels;
using FamilyMoney.Utils;
using NLog;

namespace FamilyMoney.Views;

public partial class IconPickerWindow : Window
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private byte[]? _sourceImageData;
    private string? _sourceFileName;
    private bool _fitImage;
    private bool _removeBackground;
    private double _zoom = 1;
    private double _offsetX;
    private double _offsetY;
    private int _normalizeRequest;
    private Point? _cropDragStart;
    private double _cropDragOffsetX;
    private double _cropDragOffsetY;
    private bool _syncingColorText;
    private readonly DispatcherTimer _normalizeTimer;
    private CancellationTokenSource? _normalizeCts;
    private bool _closing;

    public IconPickerWindow()
    {
        InitializeComponent();
        _normalizeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180),
        };
        _normalizeTimer.Tick += OnNormalizeTimerTick;
    }

    protected override void OnClosed(EventArgs e)
    {
        _closing = true;
        _normalizeTimer.Stop();
        _normalizeCts?.Cancel();
        _normalizeCts?.Dispose();
        _normalizeCts = null;
        base.OnClosed(e);
    }

    private async void OnNormalizeTimerTick(object? sender, EventArgs e)
    {
        _normalizeTimer.Stop();
        if (!_closing)
        {
            await NormalizeSelectedFileAsync();
        }
    }

    private void ScheduleNormalize()
    {
        if (_sourceImageData is null || _closing)
        {
            return;
        }

        _normalizeTimer.Stop();
        _normalizeTimer.Start();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (ViewModel is not { } vm)
        {
            return;
        }

        UpdateColorVisibility();

        var color = vm.SelectedColor ?? IconPalette.Colors[0];
        var index = vm.Colors.IndexOf(color);
        if (index >= 0)
        {
            ColorList.SelectedIndex = index;
        }

        if (CustomColorBox != null)
        {
            _syncingColorText = true;
            CustomColorBox.Text = vm.SelectedColor ?? string.Empty;
            _syncingColorText = false;
        }
    }

    private IconPickerViewModel? ViewModel => DataContext as IconPickerViewModel;

    private void OnIconSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is { } vm && sender is ListBox { SelectedItem: IconChoice choice })
        {
            vm.SelectIcon(choice);
        }
    }

    private void OnPackSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is { } vm && sender is ListBox { SelectedItem: PackAssetView asset })
        {
            vm.SelectPack(asset);
            ColorList.SelectedItem = null;
        }
    }

    private void OnColorSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is { } vm && sender is ListBox { SelectedItem: string color })
        {
            // Первый цвет — акцент: храним null, чтобы работал цвет по умолчанию.
            vm.SelectColor(color.Equals(IconPalette.Colors[0], StringComparison.OrdinalIgnoreCase) ? null : color);
            if (CustomColorBox != null)
            {
                _syncingColorText = true;
                CustomColorBox.Text = color;
                _syncingColorText = false;
            }
        }
    }

    private void OnCustomColorChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingColorText
            || CustomColorBox is null
            || ViewModel is not { } vm)
        {
            return;
        }

        var text = CustomColorBox.Text?.Trim() ?? string.Empty;
        if (IconColor.TryParse(text, out var normalized))
        {
            vm.SelectColor(normalized);
            ColorList.SelectedItem = null;
        }
    }

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FileStatus != null)
        {
            FileStatus.IsVisible = false;
        }

        UpdateColorVisibility();

        if (Tabs?.SelectedIndex == 2
            && ViewModel is { PreviewIcon: null, PreviewImage: null } vm
            && !string.IsNullOrWhiteSpace(vm.LetterText))
        {
            vm.SelectLetter(vm.LetterText);
        }
    }

    private void UpdateColorVisibility()
    {
        if (ColorPanel != null)
        {
            ColorPanel.IsVisible = Tabs?.SelectedIndex is 0 or 2;
        }
    }

    private async void OnChooseFile(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        var provider = GetTopLevel(this)?.StorageProvider;
        if (provider is not { CanOpen: true })
        {
            ShowFileError("Диалог выбора файла недоступен.");
            return;
        }

        try
        {
            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выбор изображения",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new("Изображения")
                    {
                        Patterns =
                        [
                            "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif",
                            "*.svg", "*.heic", "*.heif", "*.avif",
                        ],
                        MimeTypes = ["image/*"],
                    },
                ],
            });

            var file = files.FirstOrDefault();
            if (file == null)
            {
                Log.Info("IconPicker file picker returned no file");
                ShowFileInfo("Файл не выбран.");
                return;
            }

            await using var stream = await file.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var data = memory.ToArray();
            if (data.Length > 20 * 1024 * 1024)
            {
                ShowFileError("Файл больше 20 МБ. Выберите картинку поменьше.");
                return;
            }

            Log.Info("IconPicker file selected {File} ({Bytes} bytes)", file.Name, data.Length);
            _normalizeTimer.Stop();
            _normalizeCts?.Cancel();
            _sourceImageData = data;
            _sourceFileName = file.Name;
            ShowFileInfo($"Выбран {file.Name} · {FormatBytes(data.Length)}. Обрабатываю…");
            await NormalizeSelectedFileAsync();
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "IconPicker file read failed");
            ShowFileError("Не удалось прочитать файл.");
        }
    }

    private void OnFileFitChanged(object? sender, RoutedEventArgs e)
    {
        if (FitMode is null)
        {
            return;
        }

        _fitImage = FitMode.IsChecked == true;
        if (CropPanel != null)
        {
            CropPanel.IsVisible = !_fitImage;
        }

        ScheduleNormalize();
    }

    private void OnCropChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (ZoomSlider is null || OffsetXSlider is null || OffsetYSlider is null)
        {
            return;
        }

        _zoom = double.IsFinite(ZoomSlider.Value) ? ZoomSlider.Value : 1;
        _offsetX = double.IsFinite(OffsetXSlider.Value) ? OffsetXSlider.Value : 0;
        _offsetY = double.IsFinite(OffsetYSlider.Value) ? OffsetYSlider.Value : 0;
        ScheduleNormalize();
    }

    private void OnRemoveBackgroundChanged(object? sender, RoutedEventArgs e)
    {
        if (RemoveBackground is null)
        {
            return;
        }

        _removeBackground = RemoveBackground.IsChecked == true;
        ScheduleNormalize();
    }

    private void OnCropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(CropPreview).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _cropDragStart = e.GetPosition(CropPreview);
        _cropDragOffsetX = _offsetX;
        _cropDragOffsetY = _offsetY;
        e.Pointer.Capture(CropPreview);
        e.Handled = true;
    }

    private void OnCropPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_cropDragStart is not { } start || CropPreview.Bounds.Width <= 1)
        {
            return;
        }

        var position = e.GetPosition(CropPreview);
        var dx = (position.X - start.X) / CropPreview.Bounds.Width * 2;
        var dy = (position.Y - start.Y) / CropPreview.Bounds.Height * 2;
        OffsetXSlider.Value = Math.Clamp(_cropDragOffsetX - dx, -1, 1);
        OffsetYSlider.Value = Math.Clamp(_cropDragOffsetY - dy, -1, 1);
        e.Handled = true;
    }

    private void OnCropPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _cropDragStart = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnCropPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (ZoomSlider is null)
        {
            return;
        }

        ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + e.Delta.Y * 0.15, 1, 3);
        e.Handled = true;
    }

    private async Task NormalizeSelectedFileAsync()
    {
        var data = _sourceImageData;
        if (data is null || ViewModel is not { } vm || _closing)
        {
            return;
        }

        _normalizeTimer.Stop();
        _normalizeCts?.Cancel();
        _normalizeCts?.Dispose();
        _normalizeCts = new CancellationTokenSource();
        var cancellationToken = _normalizeCts.Token;
        var request = ++_normalizeRequest;
        var fileName = _sourceFileName;
        var fit = _fitImage ? ImageFitMode.Fit : ImageFitMode.Fill;
        var crop = new ImageCropSettings((float)_zoom, (float)_offsetX, (float)_offsetY);
        var removeBackground = _removeBackground;
        Log.Info(
            "IconPicker normalize request {Request}: file={File}, fit={Fit}, zoom={Zoom}, offset={OffsetX}/{OffsetY}, removeBackground={RemoveBackground}",
            request,
            fileName,
            fit,
            _zoom,
            _offsetX,
            _offsetY,
            removeBackground);
        ImageNormalizationResult result;
        try
        {
            result = await Task.Run(
                () => ImageNormalizer.Normalize(
                    data,
                    fileName,
                    fit,
                    ImageNormalizer.TargetSize,
                    crop,
                    removeBackground,
                    cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Log.Info("IconPicker normalize request {Request} canceled", request);
            return;
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "IconPicker normalization failed");
            if (request == _normalizeRequest)
            {
                ShowFileError("Не удалось обработать изображение.");
            }

            return;
        }

        if (request != _normalizeRequest || cancellationToken.IsCancellationRequested)
        {
            Log.Info("IconPicker normalize request {Request} ignored as stale", request);
            return;
        }

        if (!result.WasNormalized)
        {
            Log.Warn("IconPicker normalization error: {Error}", result.Error);
            ShowFileError(result.Error ?? "Не удалось обработать изображение.");
            return;
        }

        Log.Info(
            "IconPicker normalize request {Request} ok: {Width}x{Height}, {Bytes} bytes, warning={Warning}",
            request,
            result.Width,
            result.Height,
            result.Bytes,
            result.Warning);

        vm.SelectImage(result.Data);
        ColorList.SelectedItem = null;

        var original = result.OriginalWidth is { } width && result.OriginalHeight is { } height
            ? $"Было {width}×{height} · {FormatBytes(result.OriginalBytes)} → "
            : string.Empty;
        var background = result.BackgroundRemoved ? " · фон удалён" : string.Empty;
        var warning = string.IsNullOrWhiteSpace(result.Warning) ? string.Empty : $" · {result.Warning}";
        ShowFileInfo($"{original}{result.Width}×{result.Height} · {FormatBytes(result.Bytes)}{background}{warning}");
    }

    private static string FormatBytes(long bytes)
    {
        return bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:0.0} МБ"
            : $"{Math.Max(1, bytes / 1024)} КБ";
    }

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Clear();
        ColorList.SelectedItem = null;
        if (CustomColorBox != null)
        {
            _syncingColorText = true;
            CustomColorBox.Text = string.Empty;
            _syncingColorText = false;
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Close(ViewModel?.BuildResult());
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void ShowFileError(string message)
    {
        FileInfo.IsVisible = false;
        FileStatus.Text = message;
        FileStatus.IsVisible = true;
    }

    private void ShowFileInfo(string message)
    {
        FileStatus.IsVisible = false;
        FileInfo.Text = message;
        FileInfo.IsVisible = true;
    }
}
