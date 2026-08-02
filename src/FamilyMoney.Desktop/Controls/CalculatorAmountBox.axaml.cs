using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    private bool _applyOnNextEnter;

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

    private bool IsCalculatorOpen => CalculatorButton.Flyout is FlyoutBase { IsOpen: true };

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
        if (!IsCalculatorOpen)
        {
            CommitDisplayText();
        }
    }

    private void AmountTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F4)
        {
            OpenCalculator();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            // Consume Enter only when an expression was evaluated or the value changed.
            // Otherwise let it reach the form's IsDefault button (OK).
            if (CommitDisplayTextIfChanged())
            {
                e.Handled = true;
            }
        }
    }

    private void CalculatorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        PrepareCalculator();
    }

    private void CalculatorFlyout_OnOpened(object? sender, EventArgs e)
    {
        CalculatorPanel.Focus();
    }

    private void CalculatorPanel_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleCalculatorKey(e.Key, e.KeyModifiers, e.KeySymbol))
        {
            e.Handled = true;
        }
    }

    private void OpenCalculator()
    {
        PrepareCalculator();
        if (!IsCalculatorOpen)
        {
            CalculatorButton.Flyout?.ShowAt(CalculatorButton);
        }
    }

    private void PrepareCalculator()
    {
        CommitDisplayText();
        ResetCalculator(Value);
        _applyOnNextEnter = false;
    }

    private void CloseCalculator(bool apply)
    {
        if (apply)
        {
            ApplyCalculatorResult();
            return;
        }

        CalculatorButton.Flyout?.Hide();
        Focus();
    }

    private bool HandleCalculatorKey(Key key, KeyModifiers modifiers, string? keySymbol)
    {
        if (key == Key.Escape)
        {
            CloseCalculator(apply: false);
            return true;
        }

        if (key == Key.F4)
        {
            CloseCalculator(apply: false);
            return true;
        }

        if (key is Key.Enter or Key.Return)
        {
            if (_applyOnNextEnter || _pendingOperator is null)
            {
                CloseCalculator(apply: true);
            }
            else
            {
                ExecuteEquals();
                _applyOnNextEnter = true;
            }

            return true;
        }

        if (TryGetOperator(key, modifiers, keySymbol, out var op))
        {
            if (op == "=")
            {
                ExecuteEquals();
                _applyOnNextEnter = true;
            }
            else
            {
                ExecuteOperator(op);
                _applyOnNextEnter = false;
            }

            return true;
        }

        if (TryGetDigit(key, modifiers, out var digit))
        {
            AppendDigit(digit);
            _applyOnNextEnter = false;
            return true;
        }

        if (IsDecimalKey(key, keySymbol))
        {
            AppendDecimalSeparator();
            _applyOnNextEnter = false;
            return true;
        }

        if (key == Key.Back)
        {
            ExecuteBackspace();
            _applyOnNextEnter = false;
            return true;
        }

        if (key is Key.Delete or Key.C or Key.Clear or Key.OemClear)
        {
            ResetCalculator(0m);
            _applyOnNextEnter = false;
            return true;
        }

        return false;
    }

    private static bool TryGetDigit(Key key, KeyModifiers modifiers, out string digit)
    {
        digit = string.Empty;

        // Shift+digit is used for operators on the main keyboard (e.g. Shift+8 = *)
        if (modifiers.HasFlag(KeyModifiers.Shift)
            && key is >= Key.D0 and <= Key.D9)
        {
            return false;
        }

        digit = key switch
        {
            Key.D0 or Key.NumPad0 => "0",
            Key.D1 or Key.NumPad1 => "1",
            Key.D2 or Key.NumPad2 => "2",
            Key.D3 or Key.NumPad3 => "3",
            Key.D4 or Key.NumPad4 => "4",
            Key.D5 or Key.NumPad5 => "5",
            Key.D6 or Key.NumPad6 => "6",
            Key.D7 or Key.NumPad7 => "7",
            Key.D8 or Key.NumPad8 => "8",
            Key.D9 or Key.NumPad9 => "9",
            _ => string.Empty
        };

        return digit.Length > 0;
    }

    private static bool IsDecimalKey(Key key, string? keySymbol)
    {
        if (key is Key.OemComma or Key.OemPeriod or Key.Decimal)
        {
            return true;
        }

        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        return keySymbol == separator || keySymbol is "." or "," or "б";
    }

    private static bool TryGetOperator(Key key, KeyModifiers modifiers, string? keySymbol, out string op)
    {
        op = string.Empty;

        // Numpad operators
        if (key == Key.Add)
        {
            op = "+";
            return true;
        }

        if (key == Key.Subtract)
        {
            op = "-";
            return true;
        }

        if (key == Key.Multiply)
        {
            op = "*";
            return true;
        }

        if (key == Key.Divide)
        {
            op = "/";
            return true;
        }

        // Main keyboard: = / + on OemPlus
        if (key == Key.OemPlus)
        {
            op = modifiers.HasFlag(KeyModifiers.Shift) ? "+" : "=";
            return true;
        }

        if (key == Key.OemMinus)
        {
            op = "-";
            return true;
        }

        // Slash / question on Oem2; Shift+8 often produces *
        if (key == Key.Oem2)
        {
            op = "/";
            return true;
        }

        if (key == Key.D8 && modifiers.HasFlag(KeyModifiers.Shift))
        {
            op = "*";
            return true;
        }

        if (!string.IsNullOrEmpty(keySymbol))
        {
            op = keySymbol switch
            {
                "+" => "+",
                "-" or "−" => "-",
                "*" or "×" or "·" => "*",
                "/" or "÷" => "/",
                "=" => "=",
                _ => string.Empty
            };

            if (op.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void CalcDigit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string digit })
        {
            return;
        }

        AppendDigit(digit);
        _applyOnNextEnter = false;
        CalculatorPanel.Focus();
    }

    private void CalcDecimal_OnClick(object? sender, RoutedEventArgs e)
    {
        AppendDecimalSeparator();
        _applyOnNextEnter = false;
        CalculatorPanel.Focus();
    }

    private void CalcOperator_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string op })
        {
            return;
        }

        ExecuteOperator(op);
        _applyOnNextEnter = false;
        CalculatorPanel.Focus();
    }

    private void CalcEquals_OnClick(object? sender, RoutedEventArgs e)
    {
        ExecuteEquals();
        _applyOnNextEnter = true;
        CalculatorPanel.Focus();
    }

    private void CalcClear_OnClick(object? sender, RoutedEventArgs e)
    {
        ResetCalculator(0m);
        _applyOnNextEnter = false;
        CalculatorPanel.Focus();
    }

    private void CalcBackspace_OnClick(object? sender, RoutedEventArgs e)
    {
        ExecuteBackspace();
        _applyOnNextEnter = false;
        CalculatorPanel.Focus();
    }

    private void CalcApply_OnClick(object? sender, RoutedEventArgs e)
    {
        ApplyCalculatorResult();
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

    private void AppendDecimalSeparator()
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

    private void ExecuteOperator(string op)
    {
        ApplyPendingOperator();
        _pendingOperator = op;
        _isEnteringNewNumber = true;
        RefreshCalculatorDisplay();
    }

    private void ExecuteEquals()
    {
        ApplyPendingOperator();
        _pendingOperator = null;
        _isEnteringNewNumber = true;
        _calculatorEntry = FormatCalculatorNumber(_accumulator);
        RefreshCalculatorDisplay();
    }

    private void ExecuteBackspace()
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

    private void ApplyCalculatorResult()
    {
        ApplyPendingOperator();
        Value = RoundMoney(_accumulator);
        UpdateDisplayFromValue();
        CalculatorButton.Flyout?.Hide();
        Focus();
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

    private void CommitDisplayText() => CommitDisplayTextIfChanged();

    /// <returns>
    /// <c>true</c> if an expression was evaluated or <see cref="Value"/> changed;
    /// <c>false</c> if nothing meaningful changed (Enter can activate the default button).
    /// </returns>
    private bool CommitDisplayTextIfChanged()
    {
        var text = DisplayText?.Trim() ?? string.Empty;
        var previous = Value;

        if (string.IsNullOrEmpty(text))
        {
            if (previous == 0m)
            {
                UpdateDisplayFromValue();
                return false;
            }

            Value = 0m;
            UpdateDisplayFromValue();
            return true;
        }

        if (TryEvaluateExpression(text, out var evaluated))
        {
            Value = RoundMoney(evaluated);
            UpdateDisplayFromValue();
            return true;
        }

        text = text.Replace("б", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
        {
            var rounded = RoundMoney(parsed);
            if (rounded == previous)
            {
                UpdateDisplayFromValue();
                return false;
            }

            Value = rounded;
            UpdateDisplayFromValue();
            return true;
        }

        UpdateDisplayFromValue();
        return false;
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

            foreach (var ch in text)
            {
                if (char.IsWhiteSpace(ch))
                {
                    continue;
                }

                if (char.IsDigit(ch) || ch is '.' or ',' or 'б' || ch == decimalSeparator)
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

            void FlushNumber()
            {
                if (current.Length == 0)
                {
                    return;
                }

                var raw = current.ToString()
                    .Replace('.', decimalSeparator)
                    .Replace(',', decimalSeparator)
                    .Replace('б', decimalSeparator);

                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out var number)
                    && !decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out number))
                {
                    throw new FormatException();
                }

                numbers.Add(number);
                current.Clear();
                expectUnary = false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsBinaryOperator(string text, char decimalSeparator)
    {
        var groupSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
        text = text
            .Replace(" ", string.Empty)
            .Replace(groupSeparator, string.Empty);

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
                if (char.IsDigit(prev) || prev == decimalSeparator || prev is '.' or ',' or 'б')
                {
                    return true;
                }
            }
        }

        return false;
    }
}
