using System.Text.RegularExpressions;

namespace FamilyMoney.Voice;

public static partial class TransactionVoiceParser
{
    [GeneratedRegex(
        @"(?<key>сч[её]т|категория|подкатегория|сумма|комментарий|коммент|теги?|дата)\s+(?<value>.+?)(?=(?:\s+(?:сч[её]т|категория|подкатегория|сумма|комментарий|коммент|теги?|дата)\s+)|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CommandRegex();

    public static IReadOnlyList<TransactionVoiceCommand> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = text.Trim().Replace('ё', 'е');
        var commands = new List<TransactionVoiceCommand>();

        foreach (Match match in CommandRegex().Matches(normalized))
        {
            var key = match.Groups["key"].Value.Trim().ToLowerInvariant().Replace('ё', 'е');
            var value = match.Groups["value"].Value.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var kind = key switch
            {
                "счет" or "счёт" => TransactionVoiceCommandKind.Account,
                "категория" or "подкатегория" => TransactionVoiceCommandKind.Category,
                "сумма" => TransactionVoiceCommandKind.Sum,
                "комментарий" or "коммент" => TransactionVoiceCommandKind.Comment,
                "тег" or "теги" => TransactionVoiceCommandKind.Tag,
                "дата" => TransactionVoiceCommandKind.Date,
                _ => (TransactionVoiceCommandKind?)null,
            };

            if (kind is null)
            {
                continue;
            }

            commands.Add(new TransactionVoiceCommand(kind.Value, value));
        }

        return commands;
    }
}
