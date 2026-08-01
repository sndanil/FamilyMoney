using FamilyMoney.ViewModels;

namespace FamilyMoney.Voice;

public static class TransactionVoiceApplier
{
    public static TransactionVoiceApplyResult Apply(
        BaseTransactionViewModel viewModel,
        IReadOnlyList<TransactionVoiceCommand> commands)
    {
        var result = new TransactionVoiceApplyResult();
        if (commands.Count == 0)
        {
            return result;
        }

        var sumFromVoice = false;

        foreach (var command in commands)
        {
            switch (command.Kind)
            {
                case TransactionVoiceCommandKind.Account:
                    ApplyAccount(viewModel, command.Value, result);
                    break;
                case TransactionVoiceCommandKind.Category:
                    ApplyCategory(viewModel, command.Value, result);
                    break;
                case TransactionVoiceCommandKind.Sum:
                    if (ApplySum(viewModel, command.Value, result))
                    {
                        sumFromVoice = true;
                    }

                    break;
                case TransactionVoiceCommandKind.Comment:
                    viewModel.Comment = string.IsNullOrWhiteSpace(viewModel.Comment)
                        ? command.Value
                        : viewModel.Comment + " " + command.Value;
                    result.Applied.Add($"Комментарий: {command.Value}");
                    break;
                case TransactionVoiceCommandKind.Tag:
                    ApplyTag(viewModel, command.Value, result);
                    break;
                case TransactionVoiceCommandKind.Date:
                    ApplyDate(viewModel, command.Value, result);
                    break;
            }
        }

        if (!sumFromVoice && viewModel.Sum == 0 && viewModel.SubCategory?.LastSum > 0)
        {
            viewModel.Sum = viewModel.SubCategory.LastSum;
            result.Applied.Add($"Сумма: {viewModel.Sum:0.##}");
        }

        return result;
    }

    private static void ApplyAccount(
        BaseTransactionViewModel viewModel,
        string query,
        TransactionVoiceApplyResult result)
    {
        var accounts = viewModel.FlatAccounts?
            .Where(a => !a.IsGroup && (!a.IsHidden || a == viewModel.Account))
            ?? [];

        var account = VoiceNameMatcher.FindBest(accounts, a => a.Name, query);
        if (account == null)
        {
            result.Failed.Add($"счёт «{query}»");
            return;
        }

        viewModel.Account = account;
        result.Applied.Add($"Счёт: {account.Name}");
    }

    private static void ApplyCategory(
        BaseTransactionViewModel viewModel,
        string query,
        TransactionVoiceApplyResult result)
    {
        var subCategories = viewModel.SubCategories ?? [];
        var categories = viewModel.Categories ?? [];

        var scopedSubs = viewModel.Category == null
            ? subCategories
            : subCategories.Where(s => s.CategoryId == viewModel.Category.Id);

        var subCategory = VoiceNameMatcher.FindBest(scopedSubs, s => s.Name, query)
            ?? VoiceNameMatcher.FindBest(subCategories, s => s.Name, query);

        if (subCategory != null)
        {
            var category = categories.FirstOrDefault(c => c.Id == subCategory.CategoryId);
            if (category != null)
            {
                viewModel.Category = category;
            }

            viewModel.SubCategory = subCategory;
            viewModel.SubCategoryText = subCategory.Name;
            viewModel.Comments = subCategory.Comments ?? [];
            viewModel.RefreshSuggestedTags();

            result.Applied.Add(category != null
                ? $"Категория: {category.Name} / {subCategory.Name}"
                : $"Подкатегория: {subCategory.Name}");
            return;
        }

        var categoryOnly = VoiceNameMatcher.FindBest(categories, c => c.Name, query);
        if (categoryOnly != null)
        {
            viewModel.Category = categoryOnly;
            viewModel.SubCategory = null;
            viewModel.SubCategoryText = null;
            result.Applied.Add($"Категория: {categoryOnly.Name}");
            return;
        }

        result.Failed.Add($"категория «{query}»");
    }

    private static bool ApplySum(
        BaseTransactionViewModel viewModel,
        string query,
        TransactionVoiceApplyResult result)
    {
        if (!RussianNumberParser.TryParse(query, out var sum))
        {
            result.Failed.Add($"сумма «{query}»");
            return false;
        }

        sum = Math.Round(Math.Abs(sum), 2, MidpointRounding.AwayFromZero);
        viewModel.Sum = sum;
        if (viewModel.IsTransfer)
        {
            viewModel.ToSum = sum;
        }

        result.Applied.Add($"Сумма: {sum:0.##}");
        return true;
    }

    private static void ApplyTag(
        BaseTransactionViewModel viewModel,
        string query,
        TransactionVoiceApplyResult result)
    {
        var tags = query.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var added = new List<string>();
        foreach (var tag in tags)
        {
            if (viewModel.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            viewModel.Tags.Add(tag);
            added.Add(tag);
        }

        if (added.Count == 0)
        {
            result.Failed.Add($"тег «{query}»");
            return;
        }

        result.Applied.Add("Тег: " + string.Join(", ", added));
    }

    private static void ApplyDate(
        BaseTransactionViewModel viewModel,
        string query,
        TransactionVoiceApplyResult result)
    {
        var normalized = query.Trim().ToLowerInvariant().Replace('ё', 'е');
        var today = DateTime.Today;

        DateTime? date = normalized switch
        {
            "сегодня" => today,
            "вчера" => today.AddDays(-1),
            "позавчера" => today.AddDays(-2),
            "завтра" => today.AddDays(1),
            _ => null,
        };

        if (date == null
            && DateTime.TryParse(query, out var parsed))
        {
            date = parsed.Date;
        }

        if (date == null)
        {
            result.Failed.Add($"дата «{query}»");
            return;
        }

        viewModel.Date = date;
        result.Applied.Add($"Дата: {date:dd.MM.yyyy}");
    }
}
