using System.Text;

namespace FamilyMoney.Utils;

/// <summary>
/// Нормализация комментария операции: без лишних пробелов по краям и внутри, без учёта регистра.
/// Одна и та же функция используется и при объединении повторов в отчёте, и при фильтре
/// по позиции отчёта — иначе отобранные операции не совпали бы с тем, что посчитал отчёт.
/// </summary>
public static class CommentNormalizer
{
    public static string Normalize(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(comment.Length);
        var pendingSpace = false;

        foreach (var character in comment.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
