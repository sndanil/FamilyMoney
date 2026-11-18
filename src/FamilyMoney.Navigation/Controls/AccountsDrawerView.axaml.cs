using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Controls;

public partial class AccountsDrawerView : UserControl
{
    public AccountsDrawerView()
    {
        InitializeComponent();
    }

    private void OnAccountTapped(object? sender, TappedEventArgs e)
    {
        // Тап по стрелке раскрытия не должен выбирать группу и закрывать ящик:
        // кнопка сама переключает IsExpanded.
        if (e.Source is Visual source
            && source.FindAncestorOfType<Button>(includeSelf: true) is { } button
            && button.Classes.Contains("Expander"))
        {
            return;
        }

        if (sender is Control { DataContext: AccountViewModel account })
        {
            account.SelectCommand();

            // На узком экране ящик — оверлей. Закрываем его только после выбора
            // конкретного счёта; выбор группы оставляет ящик открытым, чтобы
            // можно было раскрыть её и выбрать вложенный счёт.
            var drawer = this.FindAncestorOfType<AppShell>()?.DrawerPage;
            if (!account.IsGroup && drawer is { DisplayMode: SplitViewDisplayMode.Overlay })
            {
                drawer.IsOpen = false;
            }
        }
    }
}
