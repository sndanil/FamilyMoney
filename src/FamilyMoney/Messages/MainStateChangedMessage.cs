using FamilyMoney.State;

namespace FamilyMoney.Messages;

public class MainStateChangedMessage
{
    public required MainState State { get; init; }

}
