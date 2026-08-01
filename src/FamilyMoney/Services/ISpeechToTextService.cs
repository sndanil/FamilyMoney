namespace FamilyMoney.Services;

/// <summary>
/// Распознавание речи средствами платформы.
/// Реализация есть на Android; на desktop сервис не регистрируется.
/// </summary>
public interface ISpeechToTextService
{
    bool IsAvailable { get; }

    /// <summary>
    /// Слушает речь и возвращает распознанный текст, либо null при отмене/ошибке.
    /// </summary>
    Task<string?> ListenAsync(CancellationToken cancellationToken = default);
}
