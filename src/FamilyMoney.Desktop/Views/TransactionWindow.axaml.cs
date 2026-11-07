using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.Utils;
using FamilyMoney.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;


namespace FamilyMoney.Views;

public partial class TransactionWindow :Window
{
    private bool _skipCategoryChange = false;

    private readonly ObservableCollection<string> _visibleCommentSuggestions = [];

    public BaseTransactionViewModel? ViewModel { get => DataContext as BaseTransactionViewModel; }

    public TransactionWindow()
    {
        InitializeComponent();

        CommentSuggestionsPopup.PlacementTarget = CommentTextBox;
        CommentSuggestionsList.ItemsSource = _visibleCommentSuggestions;

        // Enter в многострочном TextBox обрабатывается самим контролом,
        // поэтому перехватываем его на туннельной стадии.
        CommentTextBox.AddHandler(InputElement.KeyDownEvent, CommentTextBox_KeyDown, RoutingStrategies.Tunnel);
        TagCompleteBox.AddHandler(InputElement.KeyDownEvent, TagInputKeyDownTunnel, RoutingStrategies.Tunnel);

        // Отступ пунктов выпадающего списка задаём контейнеру: у закрытого ComboBox отступа нет.
        AccountComboBox.ContainerPrepared += AccountComboBox_ContainerPrepared;
        AccountComboBox.ContainerIndexChanged += AccountComboBox_ContainerIndexChanged;
        ToAccountComboBox.ContainerPrepared += AccountComboBox_ContainerPrepared;
        ToAccountComboBox.ContainerIndexChanged += AccountComboBox_ContainerIndexChanged;

        WeakReferenceMessenger.Default.Register<TransactionWindow, ModelCloseMessage<BaseTransactionViewModel>>(this, static (w, m) => w.Close(m.Result));
        WeakReferenceMessenger.Default.Register<TransactionWindow, SetFocusOnMessage>(this, (w, m) =>
        {
            switch (m.PropertyName)
            {
                case nameof(ViewModel.Sum):
                    Dispatcher.UIThread.Post(() => SumPicker.Focus());
                    break;
                default:
                    break;
            }
        });

        this.Activated += (s, e) =>
        {
            var viewModel = DataContext as BaseTransactionViewModel;
            if (viewModel == null)
            {
                return;
            }

            viewModel.RefreshSuggestedTags();

            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.Category) && !_skipCategoryChange)
                {
                    viewModel!.SubCategoryText = null;
                    viewModel!.SubCategory = null;
                    SubCategoryCompleteBox.Focus();
                }

                if (e.PropertyName is nameof(viewModel.SubCategory) or nameof(viewModel.Category))
                {
                    viewModel.RefreshSuggestedTags();
                    CloseCommentSuggestions();
                }
            };
        };

        this.Activated += TransactionWindowActivated;

        SubCategoryCompleteBox.ItemSelector = ItemSelector;
        SubCategoryCompleteBox.ItemFilter = ItemFilter;

        var sumFocused = false;
        SubCategoryCompleteBox.DropDownClosed += (s, e) =>
        {
            var viewModel = DataContext as BaseTransactionViewModel;
            if (viewModel == null)
            {
                return;
            }

            if (viewModel!.SubCategory != null)
            {
                if (viewModel!.Sum == 0)
                {
                    viewModel!.Sum = viewModel!.SubCategory.LastSum;
                }

                if (viewModel!.Category == null)
                {
                    _skipCategoryChange = true;
                    viewModel!.Category = viewModel!.Categories?.FirstOrDefault(c => c.Id == viewModel!.SubCategory.CategoryId);
                    _skipCategoryChange = false;
                }


                Task.Run(async () =>
                {
                    if (!sumFocused)
                    {
                        sumFocused = true;
                        WeakReferenceMessenger.Default.Send(new SetFocusOnMessage(nameof(viewModel.Sum)));
                    }

                    await Task.Delay(1000);
                    sumFocused = false;
                });
            }
        };
    }

    private void TransactionWindowActivated(object? sender, EventArgs e)
    {
        if (ViewModel?.Account == null)
        {
            AccountComboBox.Focus();
        }
        else if (ViewModel?.Category == null || ViewModel?.SubCategory == null)
        {
            SubCategoryCompleteBox.Focus();
        }
        else
        {
            SumPicker.Focus();
        }
    }

    private string ItemSelector(string? search, object item)
    {
        if (item is BaseSubCategoryViewModel viewModel)
        {
            return viewModel.Name;
        }

        return search ?? string.Empty;
    }

    private bool ItemFilter(string? search, object? item)
    {
        var transaction = ViewModel;

        if (item is BaseSubCategoryViewModel subCategory)
        {
            if (!string.IsNullOrEmpty(search))
            {
                return (subCategory.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                    || subCategory.Name?.Contains(KeyboardHelper.Translate(search), StringComparison.OrdinalIgnoreCase) == true
                    || subCategory.Category?.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                    || subCategory.Category?.Name?.Contains(KeyboardHelper.Translate(search), StringComparison.OrdinalIgnoreCase) == true)
                    && (transaction?.Category == null || subCategory.CategoryId == transaction?.Category.Id);
            }

            return transaction?.Category == null || transaction?.Category.Id == subCategory.CategoryId;
        }

        return true;
    }

    private void ShowDropDownButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SubCategoryCompleteBox.ShowDropDown();
    }

    private void ClearSubCategoryButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SubCategoryCompleteBox.SelectedItem = null;
        SubCategoryCompleteBox.Text = string.Empty;
    }

    private static void AccountComboBox_ContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        ApplyAccountIndent(sender as ComboBox, e.Container, e.Index);
    }

    private static void AccountComboBox_ContainerIndexChanged(object? sender, ContainerIndexChangedEventArgs e)
    {
        ApplyAccountIndent(sender as ComboBox, e.Container, e.NewIndex);
    }

    private static void ApplyAccountIndent(ComboBox? combo, Control? container, int index)
    {
        if (combo == null
            || container is not ComboBoxItem item
            || index < 0
            || index >= combo.ItemCount
            || combo.Items[index] is not AccountViewModel account)
        {
            return;
        }

        item.Padding = new Thickness(account.Level * 18d, 0d, 0d, 0d);
    }

    private void TagInputKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return) || ViewModel == null)
        {
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }

        AddPendingTag();
        e.Handled = true;
    }

    private void TagInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return) || ViewModel == null)
        {
            return;
        }

        // Shift/Alt+Enter уже обработаны на туннельной стадии.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }

        // Enter подтверждает транзакцию; недописанный тег сначала сохраняем, чтобы он не потерялся.
        AddPendingTag();

        if (ViewModel.OkCommand.CanExecute(null))
        {
            ViewModel.OkCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void AddPendingTag()
    {
        if (!string.IsNullOrWhiteSpace(ViewModel?.TagInput) && ViewModel.AddTagCommand.CanExecute(null))
        {
            ViewModel.AddTagCommand.Execute(null);
        }
    }

    private void CommentTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ToggleCommentSuggestions();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                CloseCommentSuggestions();
                return;
            }

            if (CommentSuggestionsPopup.IsOpen)
            {
                ApplySelectedComment();
            }
            else if (ViewModel?.OkCommand.CanExecute(null) == true)
            {
                ViewModel.OkCommand.Execute(null);
            }

            e.Handled = true;
            return;
        }

        if (!CommentSuggestionsPopup.IsOpen)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                MoveCommentSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveCommentSelection(-1);
                e.Handled = true;
                break;
            case Key.Escape:
                CloseCommentSuggestions();
                e.Handled = true;
                break;
        }
    }

    private void CommentTextBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        ShowCommentSuggestions();
    }

    private void CommentTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!CommentSuggestionsPopup.IsOpen)
        {
            return;
        }

        RefreshCommentSuggestions();

        if (_visibleCommentSuggestions.Count == 0)
        {
            CloseCommentSuggestions();
        }
    }

    private void CommentSuggestionsList_Tapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control control
            && (control is ListBoxItem || control.FindAncestorOfType<ListBoxItem>() != null))
        {
            ApplySelectedComment();
        }
    }

    private void CommentSuggestionsList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            ApplySelectedComment();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseCommentSuggestions();
            CommentTextBox.Focus();
            e.Handled = true;
        }
    }

    private void ToggleCommentSuggestions()
    {
        if (CommentSuggestionsPopup.IsOpen)
        {
            CloseCommentSuggestions();
            return;
        }

        ShowCommentSuggestions();
    }

    private void ShowCommentSuggestions()
    {
        if (ViewModel?.Comments is not { Count: > 0 })
        {
            CloseCommentSuggestions();
            return;
        }

        RefreshCommentSuggestions();

        if (_visibleCommentSuggestions.Count == 0)
        {
            CloseCommentSuggestions();
            return;
        }

        CommentSuggestionsPanel.Width = Math.Max(300d, CommentTextBox.Bounds.Width);
        CommentSuggestionsPopup.IsOpen = true;
        CommentSuggestionsList.SelectedIndex = 0;
        CommentSuggestionsList.ScrollIntoView(0);
    }

    private void CloseCommentSuggestions()
    {
        if (CommentSuggestionsPopup.IsOpen)
        {
            CommentSuggestionsPopup.IsOpen = false;
        }

        CommentSuggestionsList.SelectedIndex = -1;
    }

    private void RefreshCommentSuggestions()
    {
        var token = GetCommentToken();

        _visibleCommentSuggestions.Clear();

        foreach (var comment in ViewModel?.Comments ?? [])
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                continue;
            }

            if (token.Length == 0 || comment.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                _visibleCommentSuggestions.Add(comment);
            }
        }

        if (CommentSuggestionsList.SelectedIndex < 0
            || CommentSuggestionsList.SelectedIndex >= _visibleCommentSuggestions.Count)
        {
            CommentSuggestionsList.SelectedIndex = _visibleCommentSuggestions.Count > 0 ? 0 : -1;
        }
    }

    private string GetCommentToken()
    {
        var text = CommentTextBox.Text ?? string.Empty;
        var caret = Math.Clamp(CommentTextBox.CaretIndex, 0, text.Length);

        var start = caret;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        var end = caret;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        return text[start..end];
    }

    private void MoveCommentSelection(int delta)
    {
        if (_visibleCommentSuggestions.Count == 0)
        {
            return;
        }

        var index = CommentSuggestionsList.SelectedIndex;
        index = index < 0 ? 0 : Math.Clamp(index + delta, 0, _visibleCommentSuggestions.Count - 1);

        CommentSuggestionsList.SelectedIndex = index;
        CommentSuggestionsList.ScrollIntoView(index);
    }

    private void ApplySelectedComment()
    {
        if (CommentSuggestionsList.SelectedItem is not string comment || string.IsNullOrWhiteSpace(comment))
        {
            CloseCommentSuggestions();
            return;
        }

        var text = CommentTextBox.Text ?? string.Empty;
        var caret = Math.Clamp(CommentTextBox.CaretIndex, 0, text.Length);
        var selectionStart = Math.Clamp(
            Math.Min(CommentTextBox.SelectionStart, CommentTextBox.SelectionEnd), 0, text.Length);
        var selectionEnd = Math.Clamp(
            Math.Max(CommentTextBox.SelectionStart, CommentTextBox.SelectionEnd), 0, text.Length);

        var replaceStart = selectionStart;
        var replaceEnd = selectionEnd;

        // Без выделения заменяем слово под кареточной позицией — это поведение автокомплита.
        if (replaceStart == replaceEnd)
        {
            replaceStart = caret;
            while (replaceStart > 0 && !char.IsWhiteSpace(text[replaceStart - 1]))
            {
                replaceStart--;
            }

            replaceEnd = caret;
            while (replaceEnd < text.Length && !char.IsWhiteSpace(text[replaceEnd]))
            {
                replaceEnd++;
            }
        }

        var newText = text[..replaceStart] + comment + text[replaceEnd..];

        CloseCommentSuggestions();
        CommentTextBox.Text = newText;
        CommentTextBox.CaretIndex = replaceStart + comment.Length;
        CommentTextBox.SelectionStart = CommentTextBox.CaretIndex;
        CommentTextBox.SelectionEnd = CommentTextBox.CaretIndex;
        CommentTextBox.Focus();
    }

    private void TagSuggestionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.Header is string tag)
        {
            ViewModel?.SelectTagCommand.Execute(tag);
        }
    }

    private void TagCompleteBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null || TagCompleteBox.SelectedItem is not string tag)
        {
            return;
        }

        ViewModel.SelectTagCommand.Execute(tag);
        TagCompleteBox.SelectedItem = null;
    }

}
