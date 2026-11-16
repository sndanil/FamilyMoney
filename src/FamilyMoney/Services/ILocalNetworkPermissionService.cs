namespace FamilyMoney.Services;

/// <summary>
/// Разрешение на доступ к локальной сети (Android 17+).
/// На платформах без такого разрешения сервис не регистрируется.
/// </summary>
public interface ILocalNetworkPermissionService
{
    Task<bool> EnsureGrantedAsync();
}
