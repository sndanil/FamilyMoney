using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Views;

public partial class MergeSubCategoriesWindow : Window
{
    public MergeSubCategoriesWindow()
    {
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<MergeSubCategoriesWindow, ModelCloseMessage<MergeSubCategoriesViewModel>>(this, static (w, m) => w.Close(m.Result));
    }
}