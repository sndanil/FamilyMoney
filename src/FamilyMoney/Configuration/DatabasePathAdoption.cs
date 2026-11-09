namespace FamilyMoney.Configuration;

/// <summary>
/// На телефоне путь из QR компьютера не является местным файлом.
/// Вызывается до открытия базы в Android и Navigation.Desktop.
/// </summary>
public static class DatabasePathAdoption
{
    public static void Apply(IGlobalConfiguration configuration)
    {
        var root = configuration.Get();
        if (!DatabasePaths.Adopt(root, GlobalConfiguration.HomeFolder))
        {
            return;
        }

        configuration.Save(root);
    }
}
