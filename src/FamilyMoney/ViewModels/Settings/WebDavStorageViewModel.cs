using CommunityToolkit.Mvvm.ComponentModel;
using FamilyMoney.Configuration;

namespace FamilyMoney.ViewModels.Settings;

public sealed record WebDavPreset(string Title, string Url, string Hint);

public partial class WebDavStorageViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Url { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial WebDavPreset? SelectedPreset { get; set; }

    public IReadOnlyList<WebDavPreset> Presets { get; } =
    [
        new("Облако Mail.ru", "https://webdav.cloud.mail.ru", "Пароль приложения из настроек Почты"),
        new("Яндекс.Диск", "https://webdav.yandex.ru", "Нужна подписка Яндекс 360 и пароль приложения"),
        new("Nextcloud", string.Empty, "https://сервер/remote.php/dav/files/пользователь"),
        new("Другой WebDAV", string.Empty, "Адрес корневой папки WebDAV"),
    ];

    partial void OnSelectedPresetChanged(WebDavPreset? value)
    {
        if (value != null)
        {
            Url = value.Url;
        }
    }

    public void FillFrom(WebDavStorageConfiguration config)
    {
        Url = config.Url;
        UserName = config.UserName;
        Password = config.Password;
        SelectedPreset = null;
    }

    public WebDavStorageConfiguration ToConfiguration()
    {
        return new WebDavStorageConfiguration
        {
            Url = Url.Trim(),
            UserName = UserName.Trim(),
            Password = Password,
        };
    }

    public void ApplyFrom(WebDavStorageViewModel source)
    {
        Url = source.Url;
        UserName = source.UserName;
        Password = source.Password;
        SelectedPreset = null;
    }
}
