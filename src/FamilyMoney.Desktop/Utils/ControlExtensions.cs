using Avalonia.Controls;
using System;

namespace FamilyMoney.Utils;

internal static class ControlExtensions
{
    public static void ShowDropDown(this AutoCompleteBox autoCompleteBox)
    {
        if (autoCompleteBox is not null && !autoCompleteBox.IsDropDownOpen)
        {
            autoCompleteBox.InvokePrivateMethod("PopulateDropDown", autoCompleteBox, EventArgs.Empty);
            autoCompleteBox.InvokePrivateMethod("OpeningDropDown", false);

            if (!autoCompleteBox.IsDropDownOpen)
            {
                if ((bool?)autoCompleteBox.GetPrivateFieldValue("_ignorePropertyChange") == false)
                    autoCompleteBox.SetPrivateFieldValue("_ignorePropertyChange", true);

                autoCompleteBox.SetCurrentValue<bool>(AutoCompleteBox.IsDropDownOpenProperty, true);
            }
        }
    }

    private static object? InvokePrivateMethod(this object instance, string methodName, params object[] args)
    {
        if (instance is null)
        {
            return null;
        }

        return (instance.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.
            Invoke(instance, args));
    }

    private static object? GetPrivateFieldValue(this object instance, string propertyName)
    {
        if (instance is null)
        {
            return null;
        }

        return instance.GetType().GetField(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(instance);
    }

    private static void SetPrivateFieldValue(this object instance, string propertyName, object? value)
    {
        instance?.GetType().GetField(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(instance, value);
    }

    internal static object? GetPrivatePropertyValue(this object instance, string propertyName)
    {
        if (instance is null)
        {
            return null;
        }

        return instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(instance);
    }

}
