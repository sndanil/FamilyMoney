namespace FamilyMoney.Configuration;

public sealed class FolderStorageConfiguration
{
    /// <summary>
    /// Каталог обмена. Может быть локальным, сетевым или папкой, которую
    /// синхронизирует сторонний клиент. Живой файл базы сюда класть нельзя.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Path);
}
