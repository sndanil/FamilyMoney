using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FamilyMoney.ViewModels;
using System;

namespace FamilyMoney.Views;

public partial class TopExpensesView : UserControl
{
    private const double DefaultPanelWidth = 460d;

    private const double MinPanelWidth = 320d;

    private const double AnimationMs = 220d;

    private const double FrameMs = 16d;

    private TopExpensesViewModel? _viewModel;
    private DispatcherTimer? _timer;
    private double _panelWidth = DefaultPanelWidth;

    public TopExpensesView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel != null)
        {
            _viewModel.DetailsVisibilityChanged -= OnDetailsVisibilityChanged;
        }

        _viewModel = DataContext as TopExpensesViewModel;

        if (_viewModel != null)
        {
            _viewModel.DetailsVisibilityChanged += OnDetailsVisibilityChanged;
        }
    }

    /// <summary>Клик по карточке: выбрать её и открыть панель операций.</summary>
    private void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: TopExpenseCardViewModel card } && _viewModel != null)
        {
            _viewModel.SelectCard(card);
            e.Handled = true;
        }
    }

    private void OnDetailsVisibilityChanged(object? sender, EventArgs e)
    {
        var open = _viewModel?.IsDetailsOpen == true;
        var column = Body.ColumnDefinitions[2];
        var current = column.Width.IsAbsolute ? column.Width.Value : DetailsPanel.Bounds.Width;

        if (open)
        {
            // Пока колонка растёт, содержимое держим в целевой ширине — иначе текст
            // переверстывается на каждом кадре.
            DetailsContent.Width = _panelWidth;
            DetailsPanel.IsVisible = true;

            // Первый проход раскладки (шапка и все дни) может занять ощутимое время,
            // поэтому анимацию начинаем после него, иначе она целиком уйдёт в этот блок.
            Dispatcher.UIThread.Post(
                () => AnimateWidth(column, current, _panelWidth, hideOnFinish: false),
                DispatcherPriority.Loaded);
            return;
        }

        if (current >= MinPanelWidth)
        {
            _panelWidth = current;
        }

        DetailsContent.Width = current;
        AnimateWidth(column, current, 0, hideOnFinish: true);
    }

    private void AnimateWidth(ColumnDefinition column, double from, double to, bool hideOnFinish)
    {
        _timer?.Stop();
        _timer = null;

        if (Math.Abs(from - to) < 0.5d)
        {
            column.Width = new GridLength(to);
            Finish(hideOnFinish);
            return;
        }

        var start = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FrameMs) };

        timer.Tick += (_, _) =>
        {
            var progress = (DateTime.UtcNow - start).TotalMilliseconds / AnimationMs;
            if (progress >= 1d)
            {
                timer.Stop();
                if (ReferenceEquals(_timer, timer))
                {
                    _timer = null;
                }

                column.Width = new GridLength(to);
                Finish(hideOnFinish);
                return;
            }

            // CubicEaseOut: как у CollapsiblePanel в дереве.
            var eased = 1d - Math.Pow(1d - progress, 3d);
            column.Width = new GridLength(from + ((to - from) * eased));
        };

        _timer = timer;
        timer.Start();
    }

    private void Finish(bool hideOnFinish)
    {
        DetailsContent.ClearValue(WidthProperty);

        if (hideOnFinish)
        {
            DetailsPanel.IsVisible = false;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        _timer = null;

        if (_viewModel != null)
        {
            _viewModel.DetailsVisibilityChanged -= OnDetailsVisibilityChanged;
            _viewModel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }
}
