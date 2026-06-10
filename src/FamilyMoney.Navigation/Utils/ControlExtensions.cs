using Avalonia.Controls;
using System.Reflection;

namespace FamilyMoney.Navigation.Utils;

internal static class ControlExtensions
{
    public static void ShowDropDown(this AutoCompleteBox autoCompleteBox)
    {
        if (autoCompleteBox is null || autoCompleteBox.IsDropDownOpen)
        {
            return;
        }

        autoCompleteBox.InvokePrivateMethod("PopulateDropDown", autoCompleteBox, EventArgs.Empty);
        autoCompleteBox.InvokePrivateMethod("OpeningDropDown", false);

        if (!autoCompleteBox.IsDropDownOpen)
        {
            if ((bool?)autoCompleteBox.GetPrivateFieldValue("_ignorePropertyChange") == false)
            {
                autoCompleteBox.SetPrivateFieldValue("_ignorePropertyChange", true);
            }

            autoCompleteBox.SetCurrentValue(AutoCompleteBox.IsDropDownOpenProperty, true);
        }
    }

    private static object? InvokePrivateMethod(this object instance, string methodName, params object[] args)
    {
        return instance.GetType()
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)?
            .Invoke(instance, args);
    }

    private static object? GetPrivateFieldValue(this object instance, string fieldName)
    {
        return instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(instance);
    }

    private static void SetPrivateFieldValue(this object instance, string fieldName, object? value)
    {
        instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?
            .SetValue(instance, value);
    }
}
