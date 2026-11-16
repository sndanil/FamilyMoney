using Avalonia.Controls;
using Avalonia.Media.Imaging;
using FamilyMoney.ViewModels.Settings;
using QRCoder;
using System;
using System.ComponentModel;

namespace FamilyMoney;

public partial class PeerSyncWindow : Window
{
    private PeerHostViewModel? _viewModel;

    public PeerSyncWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not PeerHostViewModel viewModel)
        {
            return;
        }

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        await _viewModel.StartAsync();
        UpdateQr();
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        await _viewModel.DisposeAsync();
        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PeerHostViewModel.PairingJson))
        {
            UpdateQr();
        }
    }

    private void UpdateQr()
    {
        if (QrImage == null)
        {
            return;
        }

        var json = _viewModel?.PairingJson;
        if (string.IsNullOrWhiteSpace(json))
        {
            QrImage.Source = null;
            return;
        }

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(json, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(qrData).GetGraphic(10);
        using var stream = new System.IO.MemoryStream(png);
        QrImage.Source = new Bitmap(stream);
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Close();
}
