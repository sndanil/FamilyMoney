using FamilyMoney.Analytics;
using FamilyMoney.DataAccess;
using FamilyMoney.State;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.ViewModels;

/// <summary>Отчёт «Топ расходов»: топ по операциям-расходам.</summary>
public sealed class TopExpensesViewModel : TopOperationsViewModel
{
    public TopExpensesViewModel(
        IRepository repository,
        IStateManager stateManager,
        AccountsViewModel accounts,
        ILogger<TopOperationsViewModel> logger)
        : base(repository, stateManager, accounts, logger, TopOperationKind.Expense)
    {
    }
}
