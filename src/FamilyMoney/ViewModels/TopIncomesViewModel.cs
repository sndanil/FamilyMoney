using FamilyMoney.Analytics;
using FamilyMoney.DataAccess;
using FamilyMoney.State;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.ViewModels;

/// <summary>Отчёт «Топ доходов»: топ по операциям-доходам.</summary>
public sealed class TopIncomesViewModel : TopOperationsViewModel
{
    public TopIncomesViewModel(
        IRepository repository,
        IStateManager stateManager,
        AccountsViewModel accounts,
        ILogger<TopOperationsViewModel> logger)
        : base(repository, stateManager, accounts, logger, TopOperationKind.Income)
    {
    }
}
