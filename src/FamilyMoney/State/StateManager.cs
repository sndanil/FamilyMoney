using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
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
        WeakReferenceMessenger.Default.Send(new MainStateChangedMessage(state));
    }
}
