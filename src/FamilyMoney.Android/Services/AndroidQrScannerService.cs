using Android.Gms.Extensions;
using FamilyMoney.Services;
using Xamarin.Google.MLKit.Vision.Barcode.Common;
using Xamarin.Google.MLKit.Vision.CodeScanner;

namespace FamilyMoney.Android.Services;

/// <summary>
/// Сканирование QR-кода через Google code scanner (ML Kit).
/// Не требует разрешения на камеру: сканирование выполняется
/// внутри Google Play services.
/// </summary>
public sealed class AndroidQrScannerService : IQrScannerService
{
    public async Task<string?> ScanAsync()
    {
        var activity = MainActivity.Instance
            ?? throw new InvalidOperationException("Activity is not available.");

        var options = new GmsBarcodeScannerOptions.Builder()
            .SetBarcodeFormats(Barcode.FormatQrCode)
            .Build();

        var scanner = GmsBarcodeScanning.GetClient(activity, options);

        try
        {
            var barcode = await scanner.StartScan().AsAsync<Barcode>();
            return barcode?.RawValue;
        }
        catch (global::Android.Gms.Common.Apis.ApiException ex)
            when (ex.StatusCode == global::Android.Gms.Common.Apis.CommonStatusCodes.Canceled)
        {
            return null;
        }
    }
}
