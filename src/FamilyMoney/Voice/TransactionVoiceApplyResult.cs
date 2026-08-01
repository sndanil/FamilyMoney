namespace FamilyMoney.Voice;

public sealed class TransactionVoiceApplyResult
{
    public List<string> Applied { get; } = [];
    public List<string> Failed { get; } = [];

    public bool HasChanges => Applied.Count > 0;

    public string ToStatusText()
    {
        if (Applied.Count == 0 && Failed.Count == 0)
        {
            return "Команды не распознаны";
        }

        var parts = new List<string>();
        if (Applied.Count > 0)
        {
            parts.Add(string.Join(" · ", Applied));
        }

        if (Failed.Count > 0)
        {
            parts.Add("Не найдено: " + string.Join(", ", Failed));
        }

        return string.Join(". ", parts);
    }
}
