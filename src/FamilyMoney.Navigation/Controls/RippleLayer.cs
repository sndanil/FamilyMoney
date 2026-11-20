using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace FamilyMoney.Navigation.Controls;

/// <summary>
/// Прозрачный слой внутри кнопки, который рисует расходящийся круг
/// от точки нажатия. Используется вместо эффекта сжатия кнопки.
/// </summary>
public class RippleLayer : Canvas
{
    public static readonly StyledProperty<IBrush?> RippleBrushProperty =
        AvaloniaProperty.Register<RippleLayer, IBrush?>(nameof(RippleBrush));

    private Button? _button;

    public RippleLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    public IBrush? RippleBrush
    {
        get => GetValue(RippleBrushProperty);
        set => SetValue(RippleBrushProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _button = this.FindAncestorOfType<Button>();
        _button?.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _button?.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _button = null;

        base.OnDetachedFromVisualTree(e);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        AddRipple(e.GetPosition(this));
    }

    private void AddRipple(Point position)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        const double durationMs = 420;
        var size = Math.Max(Bounds.Width, Bounds.Height) * 1.7;
        var scale = new ScaleTransform(0.08, 0.08);
        var ripple = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = RippleBrush ?? Brushes.White,
            Opacity = 0.16,
            IsHitTestVisible = false,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = scale,
        };

        SetLeft(ripple, position.X - size / 2);
        SetTop(ripple, position.Y - size / 2);
        Children.Add(ripple);

        var start = DateTime.UtcNow;
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };

        timer.Tick += (_, _) =>
        {
            var progress = Math.Clamp((DateTime.UtcNow - start).TotalMilliseconds / durationMs, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 2);

            scale.ScaleX = 0.08 + 0.92 * eased;
            scale.ScaleY = 0.08 + 0.92 * eased;
            ripple.Opacity = 0.16 * (1 - progress);

            if (progress >= 1)
            {
                timer.Stop();
                Children.Remove(ripple);
            }
        };

        timer.Start();
    }
}
