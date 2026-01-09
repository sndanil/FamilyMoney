using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Views;

public partial class CustomPeriodWindow : Window
{
    public CustomPeriodWindow()
    {
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<CustomPeriodWindow, ModelCloseMessage<CustomPeriodViewModel>>(this, static (w, m) => w.Close(m.Result));
    }
}