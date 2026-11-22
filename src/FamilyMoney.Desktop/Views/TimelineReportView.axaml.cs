using Avalonia.Controls;
using FamilyMoney.Controls;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Views;

public partial class TimelineReportView : UserControl
{
    public TimelineReportView()
    {
        InitializeComponent();

        TimelineChart.PanRequested += (_, e) => Timeline?.PanByPixels(e.DeltaX, TimelineChart.PlotWidth);
        TimelineChart.ZoomRequested += (_, e) => Timeline?.ZoomAt(e.AnchorFraction, e.Factor);
        TimelineChart.ResetRequested += (_, _) => Timeline?.ResetToPeriod();
        TimelineChart.SizeChanged += (_, _) => PushPlotWidth();
        TimelineChart.AttachedToVisualTree += (_, _) => PushPlotWidth();
    }

    private TimelineViewModel? Timeline => DataContext as TimelineViewModel;

    private void PushPlotWidth() => Timeline?.SetPlotWidth(TimelineChart.PlotWidth);
}
