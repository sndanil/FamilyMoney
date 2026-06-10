using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.Messages;
using FamilyMoney.ViewModels.Settings;
using QRCoder;
using System.IO;

namespace FamilyMoney;

public partial class DatabaseWindow : Window
{
    public DatabaseWindow()
    {
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<DatabaseWindow, ModelCloseMessage<DatabaseViewModel>>(this, static (w, m) => w.Close(m.Result));
    }

    private void OnShowQrClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DatabaseViewModel viewModel)
        {
            return;
        }

        var json = DatabaseQrPayload.FromConfiguration(viewModel.ToConfiguration()).ToJson();

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(json, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(qrData).GetGraphic(10);

        using var stream = new MemoryStream(png);
        QrImage.Source = new Bitmap(stream);
    }
}
