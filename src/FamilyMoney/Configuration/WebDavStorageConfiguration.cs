namespace FamilyMoney.Configuration;

public sealed class WebDavStorageConfiguration
{
    /// <summary>Корневой адрес WebDAV, например https://webdav.cloud.mail.ru.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Логин. Для облаков — обычно полный адрес почты или имя пользователя.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Пароль приложения, а не основной пароль аккаунта.</summary>
    public string Password { get; set; } = string.Empty;

    public bool IsConfigured =>
        Uri.TryCreate(Url?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
