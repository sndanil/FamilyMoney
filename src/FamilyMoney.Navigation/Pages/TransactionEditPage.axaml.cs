using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.Navigation.Utils;
using FamilyMoney.Services;
using FamilyMoney.Utils;
using FamilyMoney.ViewModels;
using FamilyMoney.Voice;
using Material.Icons;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyMoney.Navigation.Pages;

public partial class TransactionEditPage : ContentPage
{
    private BaseTransactionViewModel _viewModel = null!;
    private readonly TransactionRowViewModel? _row;
    private readonly TaskCompletionSource<BaseTransactionViewModel?> _result = new();
    private readonly ISpeechToTextService? _speechToText;
    private bool _skipCategoryChange;
    private bool _closing;
    private bool _isListening;
    private int _commentInsertIndex;
    private CancellationTokenSource? _voiceStatusCts;
    private IInputPane? _inputPane;

    public TransactionEditPage()
    {
        InitializeComponent();

        _speechToText = AppInit.GlobalHost?.Services.GetService<ISpeechToTextService>();
        TextInputOptions.SetReturnKeyType(TagInputTextBox, TextInputReturnKeyType.Done);
    }

    public TransactionEditPage(BaseTransactionViewModel viewModel, TransactionRowViewModel? row)
        : this()
    {
        _row = row;
        InitializePage();
        AttachViewModel(viewModel);
        SetLoading(false);
    }

    /// <summary>
    /// Открывает страницу сразу, с пустой формой. Поля заблокированы,
    /// пока AppShell не загрузит целевую транзакцию и не вызовет <see cref="Load"/>.
    /// </summary>
    public TransactionEditPage(TransactionRowViewModel row)
        : this(row, loading: true)
    {
    }

    public TransactionEditPage(TransactionRowViewModel? row, bool loading)
        : this()
    {
        _row = row;
        InitializePage();
        AttachViewModel(new CreditTransactionViewModel { Date = DateTime.Today });
        SetLoading(loading);
    }

    private void InitializePage()
    {
        WeakReferenceMessenger.Default.Register<TransactionEditPage, ModelCloseMessage<BaseTransactionViewModel>>(
            this,
            static (page, m) => page.Close(m.Result));

        DetachedFromVisualTree += (_, _) =>
        {
            _closing = true;
            Cleanup();
            _result.TrySetResult(null);
        };
    }

    public Task<BaseTransactionViewModel?> Result => _result.Task;

    public bool IsClosed => _closing;

    public void Load(BaseTransactionViewModel viewModel)
    {
        if (_closing)
        {
            return;
        }

        AttachViewModel(viewModel);
        SetLoading(false);
    }

    private void AttachViewModel(BaseTransactionViewModel viewModel)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        DataContext = viewModel;

        var typeTitle = viewModel switch
        {
            TransferTransactionViewModel => "Перевод",
            DebetTransactionViewModel => "Доход",
            CreditTransactionViewModel => "Расход",
            _ => "Транзакция",
        };

        Header = viewModel.IsCopy ? $"Копия · {typeTitle}" : typeTitle;
        DeleteButton.IsVisible = _row != null;
        CopyButton.IsVisible = _row != null;
        VoiceTopBar.IsVisible = _speechToText?.IsAvailable == true;

        _viewModel.PropertyChanged += ViewModelPropertyChanged;
    }

    private void SetLoading(bool loading)
    {
        FormRoot.IsEnabled = !loading;
        VoiceButton.IsEnabled = !loading;

        if (loading)
        {
            Header = "Транзакция";
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _inputPane = TopLevel.GetTopLevel(this)?.InputPane;
        if (_inputPane != null)
        {
            _inputPane.StateChanged += OnInputPaneStateChanged;
            if (_inputPane.State == InputPaneState.Open)
            {
                ApplyKeyboardInset(_inputPane.OccludedRect.Height);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_inputPane != null)
        {
            _inputPane.StateChanged -= OnInputPaneStateChanged;
            _inputPane = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnInputPaneStateChanged(object? sender, InputPaneStateEventArgs e)
    {
        var height = e.NewState == InputPaneState.Open ? e.EndRect.Height : 0;
        ApplyKeyboardInset(height);

        if (height > 0)
        {
            // Поле под клавиатурой: после применения отступа прокручиваем его.
            Dispatcher.UIThread.Post(BringFocusedFieldIntoView, DispatcherPriority.Background);
        }
    }

    private void ApplyKeyboardInset(double height)
    {
        // Освобождаем не только клавиатуру, но и полосу под нижней панелью:
        // иначе панель ложится поверх поля, которое мы только что прокрутили.
        var barHeight = BottomBar.Bounds.Height > 0 ? BottomBar.Bounds.Height : 56;
        const double barGap = 12;

        DetailsScroll.Margin = new Thickness(0, 0, 0, height + barHeight + barGap);
        BottomBar.Margin = new Thickness(8, 0, 8, barGap + height);
    }

    private void BringFocusedFieldIntoView()
    {
        var field = CommentTextBox.IsFocused
            ? CommentTextBox
            : TagInputTextBox.IsFocused
                ? TagInputTextBox
                : null;

        field?.BringIntoView();
    }

    private void CommentGotFocus(object? sender, FocusChangedEventArgs e)
    {
        ScrollIntoViewAfterKeyboard(CommentTextBox);
    }

    private void TagInputGotFocus(object? sender, FocusChangedEventArgs e)
    {
        ScrollIntoViewAfterKeyboard(TagInputTextBox);
    }

    private void ScrollIntoViewAfterKeyboard(Control control)
    {
        // Окно подстраивается под клавиатуру не мгновенно: сначала даём ему
        // изменить высоту, потом прокручиваем поле в видимую область.
        DispatcherTimer.RunOnce(
            () =>
            {
                if (!_closing)
                {
                    control.BringIntoView();
                }
            },
            TimeSpan.FromMilliseconds(250));
    }

    private void Close(BaseTransactionViewModel? result)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Cleanup();
        _result.TrySetResult(result);
        Navigation?.PopAsync();
    }

    private void Cleanup()
    {
        _voiceStatusCts?.Cancel();
        _voiceStatusCts?.Dispose();
        _voiceStatusCts = null;

        _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        WeakReferenceMessenger.Default.Unregister<ModelCloseMessage<BaseTransactionViewModel>>(this);
    }

    private async void VoiceButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_speechToText == null || _isListening || _closing)
        {
            return;
        }

        _isListening = true;
        VoiceButton.IsEnabled = false;
        VoiceButtonIcon.Kind = MaterialIconKind.MicrophoneSettings;
        ShowVoiceStatus("Слушаю…", autoHide: false);

        try
        {
            var text = await _speechToText.ListenAsync();
            if (_closing)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                ShowVoiceStatus("Не удалось распознать речь. Попробуйте ещё раз.", autoHide: false);
                return;
            }

            var commands = TransactionVoiceParser.Parse(text);
            if (commands.Count == 0)
            {
                ShowVoiceStatus($"Услышано: «{text}». Команды не распознаны.", autoHide: false);
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _skipCategoryChange = true;
                try
                {
                    var applyResult = TransactionVoiceApplier.Apply(_viewModel, commands);
                    ShowVoiceStatus($"«{text}» → {applyResult.ToStatusText()}", autoHide: true);
                }
                finally
                {
                    _skipCategoryChange = false;
                }
            });
        }
        catch (Exception ex)
        {
            ShowVoiceStatus("Ошибка распознавания: " + ex.Message, autoHide: false);
        }
        finally
        {
            _isListening = false;
            VoiceButton.IsEnabled = true;
            VoiceButtonIcon.Kind = MaterialIconKind.Microphone;
        }
    }

    private void ShowVoiceStatus(string text, bool autoHide)
    {
        _voiceStatusCts?.Cancel();
        _voiceStatusCts?.Dispose();
        _voiceStatusCts = null;

        VoiceStatusText.Text = text;
        VoiceBanner.IsVisible = true;

        if (!autoHide)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _voiceStatusCts = cts;
        _ = HideVoiceStatusAsync(cts);
    }

    private async Task HideVoiceStatusAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(5000, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!cts.IsCancellationRequested && !_closing)
            {
                VoiceBanner.IsVisible = false;
            }
        });
    }

    private void CloseVoiceBannerClick(object? sender, RoutedEventArgs e)
    {
        _voiceStatusCts?.Cancel();
        VoiceBanner.IsVisible = false;
    }

    private async void DeleteConfirmClick(object? sender, RoutedEventArgs e)
    {
        var row = _row;
        Close(null);
        if (row?.Parent != null)
        {
            await row.Parent.DeleteCommand.ExecuteAsync(row);
        }
    }

    private async void CopyClick(object? sender, RoutedEventArgs e)
    {
        if (_closing)
        {
            return;
        }

        var row = _row;
        _closing = true;
        Cleanup();
        _result.TrySetResult(null);

        // Сначала закрываем текущую страницу, затем открываем копию,
        // иначе push/pop могут гоняться за одну и ту же навигацию.
        if (Navigation is { } navigation)
        {
            await navigation.PopAsync();
        }

        if (row?.Parent != null)
        {
            await row.Parent.CopyCommand.ExecuteAsync(row);
        }
    }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.Category) && !_skipCategoryChange)
        {
            _viewModel.SubCategoryText = null;
            _viewModel.SubCategory = null;
        }

        if (e.PropertyName is nameof(_viewModel.SubCategory) or nameof(_viewModel.Category))
        {
            _viewModel.RefreshSuggestedTags();
        }
    }

    private async Task<object?> PickAsync(SelectionViewModel selection)
    {
        if (Navigation is not { } navigation)
        {
            return null;
        }

        var page = new SelectionPage(selection);
        await navigation.PushAsync(page);
        return await page.Result;
    }

    private async void SelectAccountClick(object? sender, RoutedEventArgs e)
    {
        var result = await PickAsync(new SelectionViewModel(
            (_viewModel.FlatAccounts ?? []).Cast<object>(),
            _viewModel.Account,
            "Счёт",
            "Поиск счёта",
            "Счета не найдены"));

        if (result is AccountViewModel { IsGroup: false } account)
        {
            _viewModel.Account = account;
        }
    }

    private async void SelectCategoryClick(object? sender, RoutedEventArgs e)
    {
        var result = await PickAsync(new SelectionViewModel(
            (_viewModel.Categories ?? []).Cast<object>(),
            _viewModel.Category,
            "Категория",
            "Поиск категории",
            "Категории не найдены"));

        if (result is not BaseCategoryViewModel category || category.Id == _viewModel.Category?.Id)
        {
            return;
        }

        _viewModel.SubCategory = null;
        _viewModel.SubCategoryText = null;
        _viewModel.Category = category;
    }

    private async void SelectSubCategoryClick(object? sender, RoutedEventArgs e)
    {
        var category = _viewModel.Category;
        var subCategories = _viewModel.SubCategories ?? [];
        IEnumerable<BaseSubCategoryViewModel> visible = category == null
            ? subCategories
            : subCategories.Where(subCategory => subCategory.CategoryId == category.Id);

        var result = await PickAsync(new SelectionViewModel(
            visible.Cast<object>(),
            _viewModel.SubCategory,
            "Подкатегория",
            "Поиск подкатегории",
            "Подкатегории не найдены",
            allowCreate: true,
            showSubCategoryParent: category == null));

        if (result is BaseSubCategoryViewModel subCategory)
        {
            ApplySubCategory(subCategory);
        }
        else if (result is NewSubCategorySelection created)
        {
            _viewModel.SubCategory = null;
            _viewModel.SubCategoryText = created.Name;
        }
    }

    private void ApplySubCategory(BaseSubCategoryViewModel subCategory)
    {
        _skipCategoryChange = true;
        try
        {
            _viewModel.SubCategory = subCategory;
            _viewModel.SubCategoryText = subCategory.Name;
            if (_viewModel.Category == null)
            {
                _viewModel.Category = _viewModel.Categories?.FirstOrDefault(category => category.Id == subCategory.CategoryId);
            }
        }
        finally
        {
            _skipCategoryChange = false;
        }
    }

    private async void SelectToAccountClick(object? sender, RoutedEventArgs e)
    {
        var result = await PickAsync(new SelectionViewModel(
            (_viewModel.FlatAccounts ?? []).Cast<object>(),
            _viewModel.ToAccount,
            "Счёт зачисления",
            "Поиск счёта",
            "Счета не найдены"));

        if (result is AccountViewModel { IsGroup: false } account)
        {
            _viewModel.ToAccount = account;
        }
    }

    private void ClearSubCategoryClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SubCategoryText = null;
        _viewModel.SubCategory = null;
    }

    private async void OpenSumCalculator(object? sender, EventArgs e)
    {
        if (Navigation is not { } navigation)
        {
            return;
        }

        var page = new AmountCalculatorPage(SumPicker.Value);
        await navigation.PushAsync(page);
        var result = await page.Result;
        if (result.HasValue)
        {
            SumPicker.Value = result.Value;
        }
    }

    private async void OpenToSumCalculator(object? sender, EventArgs e)
    {
        if (Navigation is not { } navigation)
        {
            return;
        }

        var page = new AmountCalculatorPage(ToSumPicker.Value);
        await navigation.PushAsync(page);
        var result = await page.Result;
        if (result.HasValue)
        {
            ToSumPicker.Value = result.Value;
        }
    }

    private async void CommentDoubleTapped(object? sender, TappedEventArgs e)
    {
        _commentInsertIndex = CommentTextBox.SelectionEnd;

        var comments = (_viewModel.Comments ?? []).Cast<object>();
        var result = await PickAsync(new SelectionViewModel(
            comments,
            null,
            "Последние комментарии",
            "Поиск комментария",
            "Нет сохранённых комментариев"));

        if (result is string comment)
        {
            InsertComment(comment);
        }
    }

    private void InsertComment(string comment)
    {
        var text = _viewModel.Comment ?? string.Empty;
        var index = Math.Clamp(_commentInsertIndex, 0, text.Length);
        var insertion = comment;

        if (text.Length > 0)
        {
            if (index > 0 && text[index - 1] != '\n')
            {
                insertion = "\n" + insertion;
            }

            if (index < text.Length && text[index] != '\n' && !insertion.EndsWith('\n'))
            {
                insertion += "\n";
            }
        }

        var updated = text[..index] + insertion + text[index..];

        _viewModel.Comment = updated;
        CommentTextBox.Text = updated;
        CommentTextBox.CaretIndex = index + insertion.Length;
        CommentTextBox.Focus();
    }

    private void TagInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_viewModel.TagInput))
        {
            _viewModel.AddTagCommand.Execute(null);
        }

        e.Handled = true;
        Dispatcher.UIThread.Post(() => TagInputTextBox.Focus());
    }
}
