using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.ViewModels.Settings;

namespace FamilyMoney;

public partial class DatabaseWindow : Window
{
    public DatabaseWindow()
    {
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<DatabaseWindow, ModelCloseMessage<DatabaseViewModel>>(this, static (w, m) => w.Close(m.Result));
    }
}
