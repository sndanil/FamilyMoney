using System.Threading.Tasks;

namespace FamilyMoney.Services;

/// <summary>
/// Сканирование QR-кода средствами платформы.
/// Реализация есть только там, где доступна камера (Android).
/// </summary>
public interface IQrScannerService
{
    /// <summary>
    /// Открывает сканер и возвращает содержимое QR-кода,
    /// либо null, если пользователь отменил сканирование.
    /// </summary>
    Task<string?> ScanAsync();
}
