using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Views;

public partial class AccountsView : UserControl
{
    private Point _ghostPosition = new(0, 0);
    private readonly Point _mouseOffset = new(-5, -5);

    private readonly string customFormat = "account-view-model";

    public AccountsView()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, DragOver);
        AddHandler(DragDrop.DropEvent, Drop);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        GhostItem.IsVisible = false;
        base.OnLoaded(e);
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        var currentPosition = e.GetPosition(MainContainer);

        var offsetX = currentPosition.X - _ghostPosition.X;
        var offsetY = currentPosition.Y - _ghostPosition.Y;

        GhostItem.RenderTransform = new TranslateTransform(offsetX, offsetY);

        e.DragEffects = DragDropEffects.Move;
        if (DataContext is not AccountsViewModel vm)
            return;
        
        var data = e.Data.Get(customFormat);
        if (data is not AccountViewModel account) 
            return;
        if ((e.Source as Control)?.DataContext is not AccountViewModel destAccount)
            return;

        if (!vm.IsDestinationValid(account, destAccount))
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void Drop(object? sender, DragEventArgs e)
    {
        var data = e.Data.Get(customFormat);

        if (data is not AccountViewModel account)
        {
            return;
        }

        if (DataContext is not AccountsViewModel vm) 
            return;

        if ((e.Source as Control)?.DataContext is not AccountViewModel destAccount)
            return;

        vm.Drop(account, destAccount);
    }

    private async void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is not Control border)
            return;

        if (border.DataContext is not AccountViewModel account)
            return;

        var ghostPos = GhostItem.Bounds.Position;
        _ghostPosition = new Point(ghostPos.X + _mouseOffset.X, ghostPos.Y + _mouseOffset.Y);

        var mousePos = e.GetPosition(MainContainer);
        var offsetX = mousePos.X - ghostPos.X;
        var offsetY = mousePos.Y - ghostPos.Y + _mouseOffset.X;
        GhostItem.RenderTransform = new TranslateTransform(offsetX, offsetY);

        if (DataContext is not AccountsViewModel vm) 
            return;
        vm.DraggingAccount = account;

        GhostItem.IsVisible = true;

        var dragData = new DataObject();
        dragData.Set(customFormat, account);
        var _ = await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
        GhostItem.IsVisible = false;
    }

}
