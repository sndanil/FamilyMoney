using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using FamilyMoney.Utils;

namespace FamilyMoney.Behaviours;

public class AutoCompleteForceDropdownBehaviour : Behavior<AutoCompleteBox>
{
    protected override void OnAttached()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.KeyUp += OnKeyUp;
            AssociatedObject.DropDownOpening += DropDownOpening;
        }

        base.OnAttached();
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.KeyUp -= OnKeyUp;
            AssociatedObject.DropDownOpening -= DropDownOpening;
        }

        base.OnDetaching();
    }

    private void OnKeyUp(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if ((e.Key == Avalonia.Input.Key.Down || e.Key == Avalonia.Input.Key.F4))
        {
            if (string.IsNullOrEmpty(AssociatedObject?.Text))
            {
                AssociatedObject?.ShowDropDown();
            }
        }
    }

    private void DropDownOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var textBox = (TextBox?)AssociatedObject?.GetPrivatePropertyValue("TextBox");
        e.Cancel = textBox?.IsReadOnly == true;
    }
}
