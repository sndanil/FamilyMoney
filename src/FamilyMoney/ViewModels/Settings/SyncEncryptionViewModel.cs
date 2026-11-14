using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FamilyMoney.Configuration;
using FamilyMoney.Sync;
using System.Security.Cryptography;

namespace FamilyMoney.ViewModels.Settings;

public partial class SyncEncryptionViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool Enabled { get; set; }

    /// <summary>Вводится только для создания/проверки ключа и не сохраняется.</summary>
    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Salt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Key { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string KeyCheck { get; set; } = string.Empty;

    /// <summary>Код восстановления = сам ключ в base64. Показывается только по кнопке.</summary>
    [ObservableProperty]
    public partial string RecoveryCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    public bool HasKey => !string.IsNullOrWhiteSpace(Key);

    public bool HasPassword => !string.IsNullOrEmpty(Password);

    public bool HasRecoveryCode => !string.IsNullOrWhiteSpace(RecoveryCode);

    partial void OnKeyChanged(string value) => OnPropertyChanged(nameof(HasKey));

    partial void OnPasswordChanged(string value) => OnPropertyChanged(nameof(HasPassword));

    partial void OnRecoveryCodeChanged(string value) =>
        OnPropertyChanged(nameof(HasRecoveryCode));

    [RelayCommand]
    public void ApplyPassword() => EnsureReady();

    [RelayCommand]
    public void ShowRecoveryCode()
    {
        if (!HasKey)
        {
            Status = "Сначала задайте парольную фразу.";
            return;
        }

        RecoveryCode = Key;
        Status = "Сохраните код в надёжном месте: без него данные не восстановить.";
    }

    [RelayCommand]
    public void ApplyRecoveryCode() => ApplyRecoveryCodeCore();

    /// <summary>Проверяет/создаёт ключ. Возвращает false, если парольная фраза не подходит.</summary>
    public bool EnsureReady()
    {
        if (!Enabled)
        {
            Status = string.Empty;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(RecoveryCode))
        {
            return ApplyRecoveryCodeCore();
        }

        if (!string.IsNullOrEmpty(Password))
        {
            try
            {
                var salt = string.IsNullOrWhiteSpace(Salt)
                    ? RandomNumberGenerator.GetBytes(16)
                    : Convert.FromBase64String(Salt);
                var key = SyncCrypto.DeriveKey(Password, salt);

                if (!string.IsNullOrWhiteSpace(KeyCheck)
                    && !SyncCrypto.VerifyKeyCheck(key, KeyCheck))
                {
                    Status = "Парольная фраза не подходит к этой базе.";
                    return false;
                }

                Salt = Convert.ToBase64String(salt);
                Key = Convert.ToBase64String(key);
                KeyCheck = string.IsNullOrWhiteSpace(KeyCheck)
                    ? SyncCrypto.CreateKeyCheck(key)
                    : KeyCheck;
                Password = string.Empty;
                Status = "Ключ установлен.";
                return true;
            }
            catch (FormatException)
            {
                Status = "Повреждена соль шифрования. Задайте парольную фразу заново.";
                return false;
            }
        }

        if (HasKey)
        {
            try
            {
                var key = Convert.FromBase64String(Key);
                if (key.Length != 32
                    || (!string.IsNullOrWhiteSpace(KeyCheck)
                        && !SyncCrypto.VerifyKeyCheck(key, KeyCheck)))
                {
                    Status = "Ключ на устройстве повреждён. Введите парольную фразу или код восстановления.";
                    return false;
                }
            }
            catch (FormatException)
            {
                Status = "Ключ на устройстве повреждён. Введите парольную фразу или код восстановления.";
                return false;
            }

            Status = "Ключ установлен.";
            return true;
        }

        Status = "Введите парольную фразу.";
        return false;
    }

    private bool ApplyRecoveryCodeCore()
    {
        try
        {
            var key = Convert.FromBase64String(RecoveryCode.Trim());
            if (key.Length != 32)
            {
                Status = "Код восстановления должен быть ключом из 32 байт.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(KeyCheck)
                && !SyncCrypto.VerifyKeyCheck(key, KeyCheck))
            {
                Status = "Код восстановления не подходит к этой базе.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Salt))
            {
                Salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            }

            Key = Convert.ToBase64String(key);
            if (string.IsNullOrWhiteSpace(KeyCheck))
            {
                KeyCheck = SyncCrypto.CreateKeyCheck(key);
            }

            RecoveryCode = string.Empty;
            Status = "Ключ восстановлен.";
            return true;
        }
        catch (FormatException)
        {
            Status = "Код восстановления имеет неверный формат.";
            return false;
        }
    }

    public void FillFrom(SyncEncryptionConfiguration config)
    {
        Enabled = config.Enabled;
        Salt = config.Salt;
        Key = config.Key;
        KeyCheck = config.KeyCheck;
        Password = string.Empty;
        RecoveryCode = string.Empty;
        Status = Enabled
            ? (HasKey ? "Ключ установлен." : "Введите парольную фразу.")
            : string.Empty;
    }

    public SyncEncryptionConfiguration ToConfiguration()
    {
        return new SyncEncryptionConfiguration
        {
            Enabled = Enabled,
            Salt = Salt,
            Key = Key,
            KeyCheck = KeyCheck,
        };
    }

    public void ApplyFrom(SyncEncryptionViewModel source)
    {
        Enabled = source.Enabled;
        Salt = source.Salt;
        Key = source.Key;
        KeyCheck = source.KeyCheck;
        Password = string.Empty;
        RecoveryCode = string.Empty;
        Status = source.Status;
    }
}
