using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FamilyMoney.Controls;

public partial class CalculatorAmountBox : UserControl
{
    private const string AmountFormat = "#,##0.00";

    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<CalculatorAmountBox, decimal>(
            nameof(Value),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> DisplayTextProperty =
        AvaloniaProperty.Register<CalculatorAmountBox, string>(nameof(DisplayText), string.Empty);

    public static readonly DirectProperty<CalculatorAmountBox, string> CalculatorDisplayTextProperty =
        AvaloniaProperty.RegisterDirect<CalculatorAmountBox, string>(
            nameof(CalculatorDisplayText),
            o => o.CalculatorDisplayText);

    private decimal _accumulator;
    private string? _pendingOperator;
    private bool _isEnteringNewNumber = true;
    private string _calculatorEntry = "0";
    private string _calculatorDisplayText = "0";
    private bool _suppressDisplaySync;

    public CalculatorAmountBox()
    {
        InitializeComponent();
        UpdateDisplayFromValue();
    }

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

    public string CalculatorDisplayText
    {
        get => _calculatorDisplayText;
        private set => SetAndRaise(CalculatorDisplayTextProperty, ref _calculatorDisplayText, value);
    }

    public new bool Focus(NavigationMethod method = NavigationMethod.Unspecified, KeyModifiers keyModifiers = KeyModifiers.None)
    {
        var focused = AmountTextBox.Focus(method, keyModifiers);
        if (focused)
        {
            AmountTextBox.SelectAll();
        }

        return focused;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty && !_suppressDisplaySync)
        {
            UpdateDisplayFromValue();
        }
    }

    private void AmountTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        CommitDisplayText();
    }

    private void AmountTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitDisplayText();
            e.Handled = true;
        }
    }

    private void CalculatorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CommitDisplayText();
        ResetCalculator(Value);
    }

    private void CalcDigit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string digit })
        {
            return;
        }

        AppendDigit(digit);
    }

    private void CalcDecimal_OnClick(object? sender, RoutedEventArgs e)
    {
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

        if (_isEnteringNewNumber)
        {
            _calculatorEntry = "0" + separator;
            _isEnteringNewNumber = false;
        }
        else if (!_calculatorEntry.Contains(separator))
        {
            _calculatorEntry += separator;
        }

        RefreshCalculatorDisplay();
    }

    private void CalcOperator_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string op })
        {
            return;
        }

        ApplyPendingOperator();
        _pendingOperator = op;
        _isEnteringNewNumber = true;
        RefreshCalculatorDisplay();
    }

    private void CalcEquals_OnClick(object? sender, RoutedEventArgs e)
    {
        ApplyPendingOperator();
        _pendingOperator = null;
        _isEnteringNewNumber = true;
        _calculatorEntry = FormatCalculatorNumber(_accumulator);
        RefreshCalculatorDisplay();
    }

    private void CalcClear_OnClick(object? sender, RoutedEventArgs e)
    {
        ResetCalculator(0m);
    }

    private void CalcBackspace_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isEnteringNewNumber)
        {
            return;
        }

        if (_calculatorEntry.Length <= 1)
        {
            _calculatorEntry = "0";
            _isEnteringNewNumber = true;
        }
        else
        {
            _calculatorEntry = _calculatorEntry[..^1];
            if (_calculatorEntry is "-" or "")
            {
                _calculatorEntry = "0";
                _isEnteringNewNumber = true;
            }
        }

        RefreshCalculatorDisplay();
    }

    private void CalcApply_OnClick(object? sender, RoutedEventArgs e)
    {
        ApplyPendingOperator();
        Value = RoundMoney(_accumulator);
        UpdateDisplayFromValue();
        CalculatorButton.Flyout?.Hide();
        AmountTextBox.Focus();
    }

    private void AppendDigit(string digit)
    {
        if (_isEnteringNewNumber || _calculatorEntry == "0")
        {
            _calculatorEntry = digit;
            _isEnteringNewNumber = false;
        }
        else
        {
            _calculatorEntry += digit;
        }

        RefreshCalculatorDisplay();
    }

    private void ApplyPendingOperator()
    {
        var current = ParseCalculatorEntry();

        if (_pendingOperator is null)
        {
            _accumulator = current;
            return;
        }

        _accumulator = _pendingOperator switch
        {
            "+" => _accumulator + current,
            "-" => _accumulator - current,
            "*" => _accumulator * current,
            "/" => current == 0m ? _accumulator : _accumulator / current,
            _ => current
        };

        _accumulator = RoundMoney(_accumulator);
        _calculatorEntry = FormatCalculatorNumber(_accumulator);
    }

    private void ResetCalculator(decimal seed)
    {
        _accumulator = RoundMoney(seed);
        _pendingOperator = null;
        _isEnteringNewNumber = true;
        _calculatorEntry = FormatCalculatorNumber(_accumulator);
        RefreshCalculatorDisplay();
    }

    private void RefreshCalculatorDisplay()
    {
        CalculatorDisplayText = _calculatorEntry;
    }

    private decimal ParseCalculatorEntry()
    {
        if (decimal.TryParse(_calculatorEntry, NumberStyles.Number, CultureInfo.CurrentCulture, out var value))
        {
            return value;
        }

        return 0m;
    }

    private static string FormatCalculatorNumber(decimal value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);

    private void UpdateDisplayFromValue()
    {
        _suppressDisplaySync = true;
        DisplayText = Value.ToString(AmountFormat, CultureInfo.CurrentCulture);
        _suppressDisplaySync = false;
    }

    private void CommitDisplayText()
    {
        var text = DisplayText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            Value = 0m;
            UpdateDisplayFromValue();
            return;
        }

        if (TryEvaluateExpression(text, out var evaluated))
        {
            Value = RoundMoney(evaluated);
            UpdateDisplayFromValue();
            return;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
        {
            Value = RoundMoney(parsed);
            UpdateDisplayFromValue();
            return;
        }

        UpdateDisplayFromValue();
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Evaluates left-to-right expressions with + - * / (no operator precedence).
    /// </summary>
    private static bool TryEvaluateExpression(string text, out decimal result)
    {
        result = 0m;
        var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

        if (!ContainsBinaryOperator(text, decimalSeparator))
        {
            return false;
        }

        try
        {
            var numbers = new List<decimal>();
            var operators = new List<char>();
            var current = new StringBuilder();
            var expectUnary = true;

            void FlushNumber()
            {
                if (current.Length == 0)
                {
                    return;
                }

                var raw = current.ToString()
                    .Replace('.', decimalSeparator)
                    .Replace(',', decimalSeparator);

                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out var number)
                    && !decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out number))
                {
                    throw new FormatException();
                }

                numbers.Add(number);
                current.Clear();
                expectUnary = false;
            }

            foreach (var ch in text)
            {
                if (char.IsWhiteSpace(ch))
                {
                    continue;
                }

                if (char.IsDigit(ch) || ch is '.' or ',' || ch == decimalSeparator)
                {
                    current.Append(ch);
                    expectUnary = false;
                    continue;
                }

                if (ch is '+' or '-' or '*' or '/')
                {
                    if (expectUnary && ch is '+' or '-')
                    {
                        current.Append(ch);
                        expectUnary = false;
                        continue;
                    }

                    FlushNumber();
                    operators.Add(ch);
                    expectUnary = true;
                    continue;
                }

                return false;
            }

            FlushNumber();

            if (numbers.Count == 0 || numbers.Count != operators.Count + 1)
            {
                return false;
            }

            result = numbers[0];
            for (var i = 0; i < operators.Count; i++)
            {
                var right = numbers[i + 1];
                result = operators[i] switch
                {
                    '+' => result + right,
                    '-' => result - right,
                    '*' => result * right,
                    '/' => right == 0m ? result : result / right,
                    _ => result
                };
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsBinaryOperator(string text, char decimalSeparator)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch is '+' or '*' or '/')
            {
                return true;
            }

            if (ch == '-' && i > 0)
            {
                var prev = text[i - 1];
                if (char.IsDigit(prev) || prev == decimalSeparator || prev is '.' or ',')
                {
                    return true;
                }
            }
        }

        return false;
    }
}
