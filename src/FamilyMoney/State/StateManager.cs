using FamilyMoney.Messages;
using FamilyMoney.ViewModels;
using ReactiveUI;
using System;

namespace FamilyMoney.State;

public sealed class StateManager : IStateManager
{
    private static MainState _mainState = new ([], null, DateTime.Today, DateTime.Today.AddDays(1));

    public MainState GetMainState()
    {
        return _mainState;
    }

    public void SetMainState(MainState state)
    {
        _mainState = state;
        MessageBus.Current.SendMessage(new MainStateChangedMessage(state));
    }
}
