using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace FamilyMoney.Navigation.Controls;

/// <summary>
/// Контейнер, который плавно раскрывается и сворачивается по высоте.
/// Нужен для аккордеонов дерева: анимируется именно высота, без «мерцания» прозрачности.
/// </summary>
public class CollapsiblePanel : ContentControl
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CollapsiblePanel, bool>(nameof(IsExpanded), defaultValue: true);

    private const int FrameIntervalMs = 16;
    private const double DurationMs = 220;

    private bool _isAttached;
    private DispatcherTimer? _timer;

    static CollapsiblePanel()
    {
        IsExpandedProperty.Changed.AddClassHandler<CollapsiblePanel>((panel, _) => panel.OnIsExpandedChanged());
    }

    public CollapsiblePanel()
    {
        ClipToBounds = true;
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;

        // Начальное состояние показываем сразу, без анимации при открытии страницы.
        Height = IsExpanded ? double.NaN : 0;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        _timer?.Stop();
        _timer = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Меряем содержимое без ограничения по высоте: при свёрнутом состоянии
        // Height = 0, но для анимации раскрытия нужна полная высота.
        if (Content is Control content)
        {
            content.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }

        return base.MeasureOverride(availableSize);
    }

    private void OnIsExpandedChanged()
    {
        if (!_isAttached)
        {
            return;
        }

        _timer?.Stop();
        _timer = null;

        var current = double.IsNaN(Height) ? Bounds.Height : Height;

        if (!IsExpanded)
        {
            StartAnimation(current, 0, releaseToAuto: false);
            return;
        }

        var target = MeasureContentHeight();
        if (target <= 0)
        {
            Height = double.NaN;
            return;
        }

        // Если панель уже частично раскрыта, продолжаем с текущей высоты.
        var from = current > 0 && current < target ? current : 0;
        StartAnimation(from, target, releaseToAuto: true);
    }

    private double MeasureContentHeight()
    {
        if (Content is not Control content)
        {
            return 0;
        }

        var width = Bounds.Width > 0 ? Bounds.Width : 480;
        content.Measure(new Size(width, double.PositiveInfinity));
        return content.DesiredSize.Height;
    }

    private void StartAnimation(double from, double to, bool releaseToAuto)
    {
        var start = DateTime.UtcNow;
        Height = from;

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(FrameIntervalMs),
        };

        timer.Tick += (_, _) =>
        {
            var progress = (DateTime.UtcNow - start).TotalMilliseconds / DurationMs;
            if (progress >= 1)
            {
                timer.Stop();
                if (ReferenceEquals(_timer, timer))
                {
                    _timer = null;
                }

                Height = releaseToAuto ? double.NaN : 0;
                return;
            }

            // CubicEaseOut: быстро начали, мягко закончили.
            var eased = 1 - Math.Pow(1 - progress, 3);
            Height = from + ((to - from) * eased);
        };

        _timer = timer;
        timer.Start();
    }
}
