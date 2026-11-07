using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FamilyMoney.Analytics;
using FamilyMoney.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FamilyMoney.Controls;

/// <summary>Прокрутка таймлайна: сдвиг в пикселях.</summary>
public sealed class TimelinePanEventArgs(double deltaX) : EventArgs
{
    public double DeltaX { get; } = deltaX;
}

/// <summary>Масштабирование: доля ширины графика под курсором и кратность.</summary>
public sealed class TimelineZoomEventArgs(double anchorFraction, double factor) : EventArgs
{
    public double AnchorFraction { get; } = anchorFraction;

    public double Factor { get; } = factor;
}

/// <summary>
/// Отрисовка шкалы «Таймлайн»: две полосы (остаток и потоки), оси, подсветка периода
/// и подсказка. Контрол не хранит бизнес-состояния — только жест и позицию курсора.
/// </summary>
public sealed class TimelineControl : Control
{
    public const double ZoomFactor = 1.15d;

    private const double LeftGutter = 62d;
    private const double RightPadding = 14d;
    private const double TopPadding = 2d;
    private const double AxisRowHeight = 16d;
    private const double CaptionRowHeight = 16d;
    private const double BandTitleHeight = 15d;
    private const double BandGap = 8d;
    private const double MaxColumnWidth = 56d;

    private readonly Dictionary<(string Text, double Size, bool Bold, IBrush? Brush), FormattedText> _textCache = new();

    private bool _dragging;
    private bool _hasDragged;
    private double _lastDragX;
    private int _hoverIndex = -1;
    private double _lastPinchScale = 1d;

    static TimelineControl()
    {
        AffectsRender<TimelineControl>(
            ModelProperty,
            AccentBrushProperty,
            PositiveBrushProperty,
            NegativeBrushProperty,
            GridBrushProperty,
            SecondaryTextBrushProperty,
            TextBrushProperty,
            CardBrushProperty);
    }

    public TimelineControl()
    {
        Focusable = true;
        ClipToBounds = true;

        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(PinchEvent, OnPinch);
        AddHandler(PinchEndedEvent, OnPinchEnded);
    }

    /// <summary>Прокрутка шкалы (перетаскивание, Shift+колесо, стрелки).</summary>
    public event EventHandler<TimelinePanEventArgs>? PanRequested;

    /// <summary>Изменение масштаба (колесо, pinch, кнопки, клавиши).</summary>
    public event EventHandler<TimelineZoomEventArgs>? ZoomRequested;

    /// <summary>Сброс к периоду главного экрана (двойной клик, клавиша 0).</summary>
    public event EventHandler? ResetRequested;

    public static readonly StyledProperty<TimelineRenderModel?> ModelProperty =
        AvaloniaProperty.Register<TimelineControl, TimelineRenderModel?>(nameof(Model));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<TimelineControl, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> PositiveBrushProperty =
        AvaloniaProperty.Register<TimelineControl, IBrush?>(nameof(PositiveBrush));

    public static readonly StyledProperty<IBrush?> NegativeBrushProperty =
        AvaloniaProperty.Register<TimelineControl, IBrush?>(nameof(NegativeBrush));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<TimelineControl, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> SecondaryTextBrushProperty =
        AvaloniaProperty.Register<TimelineControl, IBrush?>(nameof(SecondaryTextBrush));

    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<TimelineControl, IBrush?>(nameof(TextBrush));

    public static readonly StyledProperty<IBrush?> CardBrushProperty =
        AvaloniaProperty.Register<TimelineControl, IBrush?>(nameof(CardBrush));

    public TimelineRenderModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public IBrush? PositiveBrush
    {
        get => GetValue(PositiveBrushProperty);
        set => SetValue(PositiveBrushProperty, value);
    }

    public IBrush? NegativeBrush
    {
        get => GetValue(NegativeBrushProperty);
        set => SetValue(NegativeBrushProperty, value);
    }

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public IBrush? SecondaryTextBrush
    {
        get => GetValue(SecondaryTextBrushProperty);
        set => SetValue(SecondaryTextBrushProperty, value);
    }

    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public IBrush? CardBrush
    {
        get => GetValue(CardBrushProperty);
        set => SetValue(CardBrushProperty, value);
    }

    /// <summary>Ширина области построения — нужна модели, чтобы выбрать шаг отсечек.</summary>
    public double PlotWidth => CreateLayout(Bounds).Plot.Width;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        var model = Model;
        if (model == null || model.Buckets.Count == 0 || bounds.Width < 80d || bounds.Height < 90d)
        {
            return;
        }

        var layout = CreateLayout(bounds);

        context.DrawRectangle(CardBrush ?? Brushes.Transparent, null, new Rect(bounds.Size));

        DrawBandTitles(context, layout);
        DrawGrid(context, model, layout);

        using (context.PushClip(layout.Plot))
        {
            DrawHighlight(context, model, layout);
            DrawBalance(context, model, layout);
            DrawFlows(context, model, layout);
        }

        DrawAxis(context, model, layout);
        DrawCaptions(context, model, layout);
        DrawHover(context, model, layout);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ModelProperty)
        {
            _hoverIndex = -1;
        }

        if (change.Property == AccentBrushProperty
            || change.Property == PositiveBrushProperty
            || change.Property == NegativeBrushProperty
            || change.Property == GridBrushProperty
            || change.Property == SecondaryTextBrushProperty
            || change.Property == TextBrushProperty
            || change.Property == CardBrushProperty)
        {
            _textCache.Clear();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed && e.Pointer.Type != PointerType.Touch)
        {
            return;
        }

        Focus();

        if (e.ClickCount == 2)
        {
            ResetRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        _dragging = true;
        _hasDragged = false;
        _lastDragX = point.Position.X;
        e.Pointer.Capture(this);
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);

        if (_dragging)
        {
            var deltaX = position.X - _lastDragX;
            if (Math.Abs(deltaX) >= 0.5d)
            {
                _hasDragged = true;
                _lastDragX = position.X;
                PanRequested?.Invoke(this, new TimelinePanEventArgs(deltaX));
            }

            return;
        }

        var index = HitTest(position);
        if (index != _hoverIndex)
        {
            _hoverIndex = index;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        Cursor = Cursor.Default;
        e.Pointer.Capture(null);

        if (!_hasDragged)
        {
            _hoverIndex = HitTest(e.GetPosition(this));
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            InvalidateVisual();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var layout = CreateLayout(Bounds);
        if (layout.Plot.Width <= 1d)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            PanRequested?.Invoke(this, new TimelinePanEventArgs(e.Delta.Y * 48d));
            e.Handled = true;
            return;
        }

        var position = e.GetPosition(this);
        var anchor = Math.Clamp((position.X - layout.Plot.Left) / layout.Plot.Width, 0d, 1d);
        var factor = e.Delta.Y > 0d ? ZoomFactor : 1d / ZoomFactor;

        ZoomRequested?.Invoke(this, new TimelineZoomEventArgs(anchor, factor));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Left:
                PanRequested?.Invoke(this, new TimelinePanEventArgs(PlotWidth * 0.1d));
                e.Handled = true;
                break;

            case Key.Right:
                PanRequested?.Invoke(this, new TimelinePanEventArgs(-PlotWidth * 0.1d));
                e.Handled = true;
                break;

            case Key.Add:
            case Key.OemPlus:
                ZoomRequested?.Invoke(this, new TimelineZoomEventArgs(0.5d, ZoomFactor));
                e.Handled = true;
                break;

            case Key.Subtract:
            case Key.OemMinus:
                ZoomRequested?.Invoke(this, new TimelineZoomEventArgs(0.5d, 1d / ZoomFactor));
                e.Handled = true;
                break;

            case Key.D0:
            case Key.NumPad0:
                ResetRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
        }
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        var scale = e.Scale <= 0d ? 1d : e.Scale;
        var factor = scale / (_lastPinchScale <= 0d ? 1d : _lastPinchScale);
        _lastPinchScale = scale;

        if (Math.Abs(factor - 1d) < 0.001d)
        {
            return;
        }

        ZoomRequested?.Invoke(this, new TimelineZoomEventArgs(0.5d, factor));
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e)
    {
        _lastPinchScale = 1d;
    }

    private int HitTest(Point position)
    {
        var model = Model;
        if (model == null)
        {
            return -1;
        }

        var layout = CreateLayout(Bounds);
        if (!layout.Plot.Contains(position))
        {
            return -1;
        }

        var totalDays = (model.To - model.From).TotalDays;
        if (totalDays <= 0d)
        {
            return -1;
        }

        // Сравниваем по датам: у дневной отсечки Start == End, и время суток в точке курсора
        // иначе никогда не попадает в интервал.
        var date = model.From.AddDays((position.X - layout.Plot.Left) / layout.Plot.Width * totalDays).Date;

        for (var i = 0; i < model.Buckets.Count; i++)
        {
            var bucket = model.Buckets[i];
            if (date >= bucket.Start.Date && date <= bucket.End.Date)
            {
                return i;
            }
        }

        return -1;
    }

    private static Layout CreateLayout(Rect bounds)
    {
        var plotTop = TopPadding;
        var axisTop = Math.Max(plotTop + 60d, bounds.Height - CaptionRowHeight - AxisRowHeight);
        var bandsHeight = Math.Max(60d, axisTop - plotTop);
        var inner = Math.Max(40d, bandsHeight - (BandTitleHeight * 2) - BandGap);
        var balanceHeight = Math.Max(24d, inner * 0.58d);

        var left = LeftGutter;
        var width = Math.Max(0d, bounds.Width - LeftGutter - RightPadding);

        var balanceTitle = new Rect(left, plotTop, width, BandTitleHeight);
        var balanceBand = new Rect(left, plotTop + BandTitleHeight, width, balanceHeight);
        var flowTitle = new Rect(left, balanceBand.Bottom + BandGap, width, BandTitleHeight);
        var flowBand = new Rect(left, flowTitle.Bottom, width, Math.Max(16d, axisTop - flowTitle.Bottom));

        return new Layout(
            new Rect(left, plotTop, width, bandsHeight),
            balanceTitle,
            balanceBand,
            flowTitle,
            flowBand,
            new Rect(left, axisTop, width, AxisRowHeight),
            new Rect(left, axisTop + AxisRowHeight, width, CaptionRowHeight));
    }

    private void DrawBandTitles(DrawingContext context, Layout layout)
    {
        var brush = SecondaryTextBrush ?? Brushes.Gray;
        context.DrawText(Text("СУММА НА СЧЁТЕ", 9d, brush), layout.BalanceTitle.Position);
        context.DrawText(Text("ПОСТУПЛЕНИЯ И СПИСАНИЯ", 9d, brush), layout.FlowTitle.Position);
    }

    private void DrawGrid(DrawingContext context, TimelineRenderModel model, Layout layout)
    {
        var grid = Alpha(GridBrush, 0.85d);
        var pen = new Pen(grid, 1d);
        var labelBrush = SecondaryTextBrush ?? Brushes.Gray;

        foreach (var tick in model.BalanceTicks)
        {
            var y = Math.Round(YBalance(tick, model, layout)) + 0.5d;
            context.DrawLine(pen, new Point(layout.BalanceBand.Left, y), new Point(layout.BalanceBand.Right, y));
            DrawAxisLabel(context, MoneyFormat.Compact(tick), layout.BalanceBand, y, labelBrush);
        }

        var zero = layout.FlowBand.Center.Y;
        var halfHeight = layout.FlowBand.Height / 2d;
        var flowMax = model.FlowMax <= 0m ? 0m : model.FlowMax;

        foreach (var tick in model.FlowTicks)
        {
            var y = Math.Round(zero - (flowMax == 0m ? 0d : ((double)(tick / flowMax) * halfHeight))) + 0.5d;
            context.DrawLine(pen, new Point(layout.FlowBand.Left, y), new Point(layout.FlowBand.Right, y));
            DrawAxisLabel(context, MoneyFormat.Compact(tick), layout.FlowBand, y, labelBrush);
        }

        var zeroPen = new Pen(Alpha(SecondaryTextBrush, 0.55d), 1d);
        context.DrawLine(
            zeroPen,
            new Point(layout.FlowBand.Left, Math.Round(zero) + 0.5d),
            new Point(layout.FlowBand.Right, Math.Round(zero) + 0.5d));

        // Вертикальные отсечки: у границ года/месяца заметнее.
        var softPen = new Pen(Alpha(GridBrush, 0.45d), 1d);
        var hardPen = new Pen(Alpha(GridBrush, 1d), 1d);

        foreach (var bucket in model.Buckets)
        {
            var x = Math.Round(X(bucket.Start, model, layout)) + 0.5d;
            var isBoundary = model.Scale switch
            {
                TimelineScale.Years => true,
                TimelineScale.Months => bucket.Start.Month == 1,
                _ => bucket.Start.Day == 1,
            };

            context.DrawLine(
                isBoundary ? hardPen : softPen,
                new Point(x, layout.Plot.Top),
                new Point(x, layout.Plot.Bottom));
        }
    }

    private void DrawAxisLabel(DrawingContext context, string value, Rect band, double y, IBrush brush)
    {
        var text = Text(value, 10d, brush);
        var labelY = Math.Clamp(y - (text.Height / 2d), band.Top, band.Bottom - text.Height);
        context.DrawText(text, new Point(band.Left - 10d - text.Width, labelY));
    }

    private void DrawHighlight(DrawingContext context, TimelineRenderModel model, Layout layout)
    {
        if (model.HighlightFrom is not { } from || model.HighlightTo is not { } to)
        {
            return;
        }

        var viewDays = (model.To - model.From).TotalDays;
        var highlightDays = (to.Date - from.Date).Days + 1;
        if (highlightDays >= viewDays)
        {
            return;
        }

        var left = X(from, model, layout);
        var right = X(to.AddDays(1), model, layout);
        var rect = new Rect(left, layout.Plot.Top, Math.Max(1d, right - left), layout.Plot.Height);

        context.DrawRectangle(Alpha(AccentBrush, 0.06d), null, rect);
        context.DrawRectangle(new Pen(Alpha(AccentBrush, 0.22d), 1d), rect);
    }

    private void DrawBalance(DrawingContext context, TimelineRenderModel model, Layout layout)
    {
        var fill = Alpha(AccentBrush, 0.55d);
        var points = new List<Point>(model.Buckets.Count);

        foreach (var bucket in model.Buckets)
        {
            var left = X(bucket.Start, model, layout);
            var right = X(bucket.End.AddDays(1), model, layout);
            var width = Math.Clamp((right - left) * 0.56d, 1d, MaxColumnWidth);
            var center = (left + right) / 2d;
            var y = YBalance(bucket.Balance, model, layout);

            context.DrawRectangle(fill, null, new Rect(center - (width / 2d), y, width, layout.BalanceBand.Bottom - y), 2d, 2d);
            points.Add(new Point(center, y));
        }

        if (points.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(points[0], false);
            for (var i = 1; i < points.Count; i++)
            {
                stream.LineTo(points[i]);
            }

            stream.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(AccentBrush ?? Brushes.DodgerBlue, 2d), geometry);
    }

    private void DrawFlows(DrawingContext context, TimelineRenderModel model, Layout layout)
    {
        var positive = PositiveBrush ?? Brushes.Green;
        var negative = NegativeBrush ?? Brushes.Red;
        var zero = layout.FlowBand.Center.Y;
        var half = layout.FlowBand.Height / 2d;
        var flowMax = model.FlowMax <= 0m ? 1m : model.FlowMax;

        foreach (var bucket in model.Buckets)
        {
            var left = X(bucket.Start, model, layout);
            var right = X(bucket.End.AddDays(1), model, layout);
            var width = Math.Clamp((right - left) * 0.30d, 1d, 28d);
            var center = (left + right) / 2d;

            var incomeHeight = (double)(bucket.Income / flowMax) * half;
            if (incomeHeight >= 0.5d)
            {
                context.DrawRectangle(
                    positive,
                    null,
                    new Rect(center - width - 0.5d, zero - incomeHeight, width, incomeHeight),
                    1.5d,
                    1.5d);
            }

            var expenseHeight = (double)(bucket.Expense / flowMax) * half;
            if (expenseHeight >= 0.5d)
            {
                context.DrawRectangle(
                    negative,
                    null,
                    new Rect(center + 0.5d, zero, width, expenseHeight),
                    1.5d,
                    1.5d);
            }
        }
    }

    private void DrawAxis(DrawingContext context, TimelineRenderModel model, Layout layout)
    {
        var brush = SecondaryTextBrush ?? Brushes.Gray;
        var step = layout.Plot.Width / Math.Max(1, model.Buckets.Count);
        var every = Math.Max(1, (int)Math.Ceiling(44d / Math.Max(1d, step)));

        for (var i = 0; i < model.Buckets.Count; i++)
        {
            if (i % every != 0)
            {
                continue;
            }

            var bucket = model.Buckets[i];
            var text = Text(bucket.Label, 10d, brush);
            if (text.Width > step * every - 4d)
            {
                continue;
            }

            var center = (X(bucket.Start, model, layout) + X(bucket.End.AddDays(1), model, layout)) / 2d;
            context.DrawText(text, new Point(center - (text.Width / 2d), layout.AxisRow.Top + 2d));
        }
    }

    private void DrawCaptions(DrawingContext context, TimelineRenderModel model, Layout layout)
    {
        var brush = SecondaryTextBrush ?? Brushes.Gray;
        var pen = new Pen(Alpha(GridBrush, 0.9d), 1d);

        foreach (var caption in model.Captions)
        {
            var left = X(caption.From, model, layout);
            var right = X(caption.To.AddDays(1), model, layout);
            if (right - left < 24d)
            {
                continue;
            }

            var y = Math.Round(layout.CaptionRow.Top) + 0.5d;
            context.DrawLine(pen, new Point(left, y), new Point(right, y));

            var text = Text(caption.Text, 10d, brush);
            if (text.Width > right - left - 8d)
            {
                continue;
            }

            context.DrawText(text, new Point(((left + right) / 2d) - (text.Width / 2d), y + 2d));
        }
    }

    private void DrawHover(DrawingContext context, TimelineRenderModel model, Layout layout)
    {
        if (_hoverIndex < 0 || _hoverIndex >= model.Buckets.Count)
        {
            return;
        }

        var bucket = model.Buckets[_hoverIndex];
        var left = X(bucket.Start, model, layout);
        var right = X(bucket.End.AddDays(1), model, layout);
        var center = (left + right) / 2d;

        context.DrawRectangle(Alpha(Brushes.White, 0.05d), null, new Rect(left, layout.Plot.Top, Math.Max(1d, right - left), layout.Plot.Height));

        var accentLine = new Pen(Alpha(AccentBrush, 0.5d), 1d);
        context.DrawLine(accentLine, new Point(center, layout.Plot.Top), new Point(center, layout.Plot.Bottom));

        var dotY = YBalance(bucket.Balance, model, layout);
        context.DrawEllipse(AccentBrush ?? Brushes.DodgerBlue, new Pen(CardBrush ?? Brushes.Black, 2d), new Point(center, dotY), 4d, 4d);

        DrawTooltip(context, bucket, model, layout, left, right);
    }

    private void DrawTooltip(DrawingContext context, TimelineBucket bucket, TimelineRenderModel model, Layout layout, double left, double right)
    {
        const double width = 218d;
        const double padding = 12d;
        const double rowHeight = 18d;

        var rows = new List<(string Name, string Value, IBrush Brush)>
        {
            ("Баланс", MoneyFormat.Full(bucket.Balance), TextBrush ?? Brushes.White),
            ("Доходы", MoneyFormat.Full(bucket.Income), PositiveBrush ?? Brushes.Green),
            ("Расходы", MoneyFormat.Full(-bucket.Expense), NegativeBrush ?? Brushes.Red),
        };

        if (bucket.Transfers != 0m)
        {
            rows.Add(("в т.ч. переводы", MoneyFormat.Signed(bucket.Transfers), SecondaryTextBrush ?? Brushes.Gray));
        }

        rows.Add(("Изменение", MoneyFormat.Signed(bucket.Change), (bucket.Change >= 0m ? PositiveBrush : NegativeBrush) ?? Brushes.Gray));

        var height = padding + 18d + (rows.Count * rowHeight) + padding;

        var x = right + 12d;
        if (x + width > layout.Plot.Right - 4d)
        {
            x = left - 12d - width;
        }

        x = Math.Max(layout.Plot.Left + 4d, x);
        var y = layout.Plot.Top + 6d;

        var rect = new Rect(x, y, width, height);
        context.DrawRectangle(
            CardBrush ?? Brushes.Black,
            new Pen(Alpha(GridBrush, 1d), 1d),
            rect,
            8d,
            8d);

        context.DrawText(Text(bucket.Title, 12d, TextBrush ?? Brushes.White, bold: true), new Point(x + padding, y + padding));

        var rowY = y + padding + 20d;
        foreach (var (name, value, brush) in rows)
        {
            context.DrawText(Text(name, 11d, SecondaryTextBrush ?? Brushes.Gray), new Point(x + padding, rowY));

            var valueText = Text(value, 11d, brush);
            context.DrawText(valueText, new Point(x + width - padding - valueText.Width, rowY));

            rowY += rowHeight;
        }
    }

    private static double X(DateTime date, TimelineRenderModel model, Layout layout)
    {
        var total = (model.To - model.From).TotalDays;
        if (total <= 0d)
        {
            return layout.Plot.Left;
        }

        return layout.Plot.Left + (((date - model.From).TotalDays / total) * layout.Plot.Width);
    }

    private static double YBalance(decimal value, TimelineRenderModel model, Layout layout)
    {
        var span = model.BalanceCeiling - model.BalanceFloor;
        if (span <= 0m)
        {
            return layout.BalanceBand.Bottom;
        }

        var ratio = (double)((value - model.BalanceFloor) / span);
        return layout.BalanceBand.Bottom - (ratio * layout.BalanceBand.Height);
    }

    private IBrush Alpha(IBrush? source, double opacity)
    {
        if (source is ISolidColorBrush solid)
        {
            var color = solid.Color;
            return new SolidColorBrush(Color.FromArgb((byte)(color.A * opacity), color.R, color.G, color.B));
        }

        return source ?? Brushes.Gray;
    }

    private FormattedText Text(string text, double size, IBrush? brush, bool bold = false)
    {
        var key = (text, size, bold, brush);
        if (_textCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var typeface = new Typeface(
            FontFamily.Default,
            FontStyle.Normal,
            bold ? FontWeight.SemiBold : FontWeight.Normal);

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush ?? Brushes.White);

        if (_textCache.Count > 512)
        {
            _textCache.Clear();
        }

        _textCache[key] = formatted;
        return formatted;
    }

    private readonly record struct Layout(
        Rect Plot,
        Rect BalanceTitle,
        Rect BalanceBand,
        Rect FlowTitle,
        Rect FlowBand,
        Rect AxisRow,
        Rect CaptionRow);
}
