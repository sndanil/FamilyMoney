using Avalonia.Controls;
using FamilyMoney.Controls;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();

        TimelineChart.PanRequested += (_, e) => Timeline?.PanByPixels(e.DeltaX, TimelineChart.PlotWidth);
        TimelineChart.ZoomRequested += (_, e) => Timeline?.ZoomAt(e.AnchorFraction, e.Factor);
        TimelineChart.ResetRequested += (_, _) => Timeline?.ResetToPeriod();
        TimelineChart.SizeChanged += (_, _) => PushPlotWidth();
        TimelineChart.AttachedToVisualTree += (_, _) => PushPlotWidth();
    }

    private TimelineViewModel? Timeline => (DataContext as ReportsViewModel)?.Timeline;

    private void PushPlotWidth() => Timeline?.SetPlotWidth(TimelineChart.PlotWidth);
}
