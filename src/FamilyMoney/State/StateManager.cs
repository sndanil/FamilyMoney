using FamilyMoney.Messages;
using FamilyMoney.ViewModels;
using ReactiveUI;
using System;

namespace FamilyMoney.State;

public sealed class StateManager : IStateManager
{
    private static MainState _mainState = new()
    { 
        Accounts = Array.Empty<AccountViewModel>(), 
        PeriodFrom = DateTime.Today, 
        PeriodTo = DateTime.Today.AddDays(1) 
    };

    public MainState GetMainState()
    {
        return _mainState;
    }

    public void SetMainState(MainState state)
    {
        _mainState = state;
        MessageBus.Current.SendMessage(new MainStateChangedMessage { State = state });
    }
}
