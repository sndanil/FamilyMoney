using Avalonia;
using Avalonia.Controls;
using FamilyMoney.Navigation.Pages;

namespace FamilyMoney.Navigation;

public partial class MainView : NavigationPage
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (CurrentPage == null)
            await PushAsync(new HomePage()
            {
                DataContext = DataContext
            });
    }
}
