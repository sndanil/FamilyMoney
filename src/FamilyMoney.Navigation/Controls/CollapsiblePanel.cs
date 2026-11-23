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
        AvaloniaProperty.Register<CollapsiblePanel, bool>(nameof(IsExpanded), defaultValue: false);

    private const int FrameIntervalMs = 16;
    private const double DurationMs = 220;

    private bool _isAttached;
    private DispatcherTimer? _timer;

    // Нужно ли сейчас измерять и раскладывать содержимое. У свёрнутой панели оно
    // не живёт: иначе ItemsControl создаёт контейнеры всех скрытых строк и дерево
    // в режиме «Год» строит тысячи визуалов на каждое переключение периода.
    private bool _contentLive;

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
        _contentLive = IsExpanded;
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
        if (!_contentLive)
        {
            return default;
        }

        // Меряем содержимое без ограничения по высоте: при раскрытии Height = auto,
        // но для анимации нужна полная высота содержимого.
        if (Content is Control content)
        {
            content.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }

        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Скрытое содержимое не раскладываем: именно раскладка заставляет
        // ItemsControl создавать контейнеры всех строк, даже когда узел закрыт.
        if (!_contentLive)
        {
            return finalSize;
        }

        return base.ArrangeOverride(finalSize);
    }

    private void OnIsExpandedChanged()
    {
        if (!_isAttached)
        {
            _contentLive = IsExpanded;
            return;
        }

        _timer?.Stop();
        _timer = null;

        if (!IsExpanded)
        {
            // Содержимое остаётся живым до конца анимации сворачивания, чтобы
            // уезжало под обрез, а не исчезало мгновенно.
            var current = double.IsNaN(Height) ? Bounds.Height : Height;
            StartAnimation(current, 0, releaseToAuto: false);
            return;
        }

        _contentLive = true;
        InvalidateMeasure();
        InvalidateArrange();

        var target = MeasureContentHeight();
        if (target <= 0)
        {
            Height = double.NaN;
            return;
        }

        // Если панель уже частично раскрыта, продолжаем с текущей высоты.
        var from = double.IsNaN(Height)
            ? 0
            : Height > 0 && Height < target ? Height : 0;
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

                if (releaseToAuto)
                {
                    Height = double.NaN;
                    return;
                }

                Height = 0;

                // Свернулись: содержимое больше не требуется — снимаем нагрузку
                // с раскладки и не создаём контейнеры скрытых строк.
                _contentLive = false;
                InvalidateMeasure();
                InvalidateArrange();
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
