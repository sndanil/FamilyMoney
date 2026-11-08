using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;

namespace FamilyMoney.Navigation.Controls;

public partial class AmountEntry : UserControl
{
    private const string AmountFormat = "#,##0.00";

    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<AmountEntry, decimal>(
            nameof(Value),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> DisplayTextProperty =
        AvaloniaProperty.Register<AmountEntry, string>(nameof(DisplayText), string.Empty);

    private bool _suppressDisplaySync;

    public AmountEntry()
    {
        InitializeComponent();

        TextInputOptions.SetContentType(AmountTextBox, TextInputContentType.Number);
        TextInputOptions.SetReturnKeyType(AmountTextBox, TextInputReturnKeyType.Done);

        UpdateDisplayFromValue();
    }

    public event EventHandler? CalculatorRequested;

    public decimal Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string DisplayText
    {
        get => GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    public void Commit()
    {
        CommitDisplayText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty && !_suppressDisplaySync)
        {
            UpdateDisplayFromValue();
        }
    }

    private void AmountTextBox_OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        AmountTextBox.SelectAll();
    }

    private void AmountTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressDisplaySync)
        {
            return;
        }

        var text = (AmountTextBox.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            SetValueFromText(0m);
            return;
        }

        if (TryParse(text, out var parsed))
        {
            SetValueFromText(Math.Round(parsed, 2, MidpointRounding.AwayFromZero));
        }
    }

    private void AmountTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        CommitDisplayText();
    }

    private void AmountTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            CommitDisplayText();
            e.Handled = true;
        }
    }

    private void CalculatorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CommitDisplayText();
        CalculatorRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateDisplayFromValue()
    {
        _suppressDisplaySync = true;
        DisplayText = Value.ToString(AmountFormat, CultureInfo.CurrentCulture);
        _suppressDisplaySync = false;
    }

    private void CommitDisplayText()
    {
        var text = (AmountTextBox.Text ?? DisplayText ?? string.Empty).Trim();

        if (text.Length == 0)
        {
            SetValueFromText(0m);
            UpdateDisplayFromValue();
            return;
        }

        if (TryParse(text, out var parsed))
        {
            SetValueFromText(Math.Round(parsed, 2, MidpointRounding.AwayFromZero));
        }

        UpdateDisplayFromValue();
    }

    private void SetValueFromText(decimal value)
    {
        _suppressDisplaySync = true;
        Value = value;
        _suppressDisplaySync = false;
    }

    private static bool TryParse(string text, out decimal value)
    {
        var normalized = text
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty);

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
