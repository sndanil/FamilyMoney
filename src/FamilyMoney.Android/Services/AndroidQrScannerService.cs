using AndroidTask = global::Android.Gms.Tasks.Task;
using AndroidTaskListener = global::Android.Gms.Tasks.IOnCompleteListener;
using FamilyMoney.Services;
using Java.Lang;
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

        // Не используем AsAsync: его слушатель вызывает SetException(null), когда
        // задачу отменяют закрытием сканера, и это роняет процесс из Java-колбэка.
        var completion = new TaskCompletionSource<Barcode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        scanner.StartScan().AddOnCompleteListener(new BarcodeCompleteListener(completion));

        try
        {
            var barcode = await completion.Task.ConfigureAwait(false);
            return barcode?.RawValue;
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    private sealed class BarcodeCompleteListener : Java.Lang.Object, AndroidTaskListener
    {
        private readonly TaskCompletionSource<Barcode?> _completion;

        public BarcodeCompleteListener(TaskCompletionSource<Barcode?> completion)
        {
            _completion = completion;
        }

        public void OnComplete(AndroidTask task)
        {
            if (task.IsSuccessful)
            {
                _completion.TrySetResult(task.Result as Barcode);
                return;
            }

            // Закрытие сканера без кода — это отмена, а не ошибка.
            if (task.IsCanceled)
            {
                _completion.TrySetResult(null);
                return;
            }

            var error = task.Exception;
            _completion.TrySetException(error is null
                ? new InvalidOperationException("QR scan failed.")
                : error);
        }
    }
}
