using CommunityToolkit.Mvvm.Messaging.Messages;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Messages;

public class ModelEditMessage<T> : AsyncRequestMessage<T?> where T : ViewModelBase
{
    public T? From { get; init; }

    public ModelEditMessage(T from)
    {
        From = from;
    }

    public ModelEditMessage()
    {
        From = null;
    }
}
    
public record ModelCloseMessage<T>(T? Result);
