using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FamilyMoney.Navigation.Pages;

public partial class AmountCalculatorPage : ContentPage
{
    private readonly TaskCompletionSource<decimal?> _result = new();
    private decimal _accumulator;
    private string? _pendingOperator;
    private string _entry;
    private bool _isEnteringNewNumber = true;

    public AmountCalculatorPage()
    {
        InitializeComponent();
        _entry = "0";
        UpdateDisplay();
    }

    public AmountCalculatorPage(decimal initialValue)
        : this()
    {
        _accumulator = Round(initialValue);
        _entry = Format(_accumulator);
        _isEnteringNewNumber = true;
        UpdateDisplay();

        DetachedFromVisualTree += (_, _) => _result.TrySetResult(null);
    }

    public Task<decimal?> Result => _result.Task;

    private void DigitClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string digit })
        {
            return;
        }

        if (_isEnteringNewNumber || _entry == "0")
        {
            _entry = digit;
            _isEnteringNewNumber = false;
        }
        else
        {
            _entry += digit;
        }

        UpdateDisplay();
    }

    private void DecimalClick(object? sender, RoutedEventArgs e)
    {
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

        if (_isEnteringNewNumber)
        {
            _entry = "0" + separator;
            _isEnteringNewNumber = false;
        }
        else if (!_entry.Contains(separator))
        {
            _entry += separator;
        }

        UpdateDisplay();
    }

    private void OperatorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string op })
        {
            return;
        }

        ApplyPendingOperator();
        _pendingOperator = op;
        _isEnteringNewNumber = true;
        UpdateDisplay();
    }

    private void EqualsClick(object? sender, RoutedEventArgs e)
    {
        ApplyPendingOperator();
        _pendingOperator = null;
        _isEnteringNewNumber = true;
        _entry = Format(_accumulator);
        UpdateDisplay();
    }

    private void ClearClick(object? sender, RoutedEventArgs e)
    {
        _accumulator = 0m;
        _pendingOperator = null;
        _entry = "0";
        _isEnteringNewNumber = true;
        UpdateDisplay();
    }

    private void BackspaceClick(object? sender, RoutedEventArgs e)
    {
        if (_isEnteringNewNumber)
        {
            return;
        }

        if (_entry.Length <= 1)
        {
            _entry = "0";
            _isEnteringNewNumber = true;
        }
        else
        {
            _entry = _entry[..^1];
        }

        UpdateDisplay();
    }

    private void ApplyClick(object? sender, RoutedEventArgs e)
    {
        ApplyPendingOperator();
        var result = Round(_accumulator);
        _result.TrySetResult(result);
        Navigation?.PopAsync();
    }

    private void ApplyPendingOperator()
    {
        var current = ParseEntry();

        if (_pendingOperator is null)
        {
            _accumulator = current;
            _entry = Format(_accumulator);
            return;
        }

        _accumulator = _pendingOperator switch
        {
            "+" => _accumulator + current,
            "-" => _accumulator - current,
            "*" => _accumulator * current,
            "/" => current == 0m ? _accumulator : _accumulator / current,
            _ => current,
        };

        _accumulator = Round(_accumulator);
        _entry = Format(_accumulator);
    }

    private decimal ParseEntry()
    {
        return decimal.TryParse(_entry, NumberStyles.Number, CultureInfo.CurrentCulture, out var value)
            ? value
            : 0m;
    }

    private static string Format(decimal value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private void UpdateDisplay()
    {
        DisplayText.Text = _entry;
    }
}
