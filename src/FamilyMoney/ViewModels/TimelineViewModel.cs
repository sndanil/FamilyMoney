using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Analytics;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.State;
using FamilyMoney.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FamilyMoney.ViewModels;

/// <summary>
/// Состояние шкалы «Таймлайн»: выбранный счёт, видимое окно, шаг отсечек и готовый набор
/// данных для контрола. Вся арифметика — в <see cref="Analytics"/>, здесь только состояние.
/// </summary>
public partial class TimelineViewModel : ViewModelBase
{
    /// <summary>Минимальная ширина стартового окна (период «день» иначе даёт одну отсечку).</summary>
    private const int MinStartWindowDays = 14;

    private const int MinZoomDays = 3;

    private const double ZoomStep = 1.15d;

    private static readonly DateTime HistoryFrom = new(1900, 1, 1);

    private static readonly DateTime HistoryTo = new(2100, 12, 31);

    private readonly IRepository _repository;
    private readonly IStateManager _stateManager;
    private readonly AccountsViewModel _accounts;
    private readonly ILogger<TimelineViewModel> _logger;

    private DailySeries _series = DailySeries.Empty;
    private DateTime _boundsFrom = DateTime.Today.AddDays(-MinStartWindowDays);
    private DateTime _boundsTo = DateTime.Today;
    private DateTime _viewFrom = DateTime.Today.AddDays(-MinStartWindowDays);
    private DateTime _viewTo = DateTime.Today;
    private DateTime _highlightFrom = DateTime.Today;
    private DateTime _highlightTo = DateTime.Today;
    private DateTime _syncedPeriodFrom = DateTime.MinValue;
    private DateTime _syncedPeriodTo = DateTime.MinValue;
    private double _plotWidth = 900d;
    private int _loadVersion;
    private bool _initialized;
    private bool _suppressScopeChange;

    public TimelineViewModel(
        IRepository repository,
        IStateManager stateManager,
        AccountsViewModel accounts,
        ILogger<TimelineViewModel> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        SubscribeMessages();
    }

    public ObservableCollection<TimelineScope> Scopes { get; } = [];

    [ObservableProperty]
    public partial TimelineScope? SelectedScope { get; set; }

    [ObservableProperty]
    public partial TimelineRenderModel? RenderModel { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? EmptyText { get; set; }

    [ObservableProperty]
    public partial string EndBalanceText { get; set; } = string.Empty;

    /// <summary>
    /// Вызывается при входе в раздел: обновляет список счетов, берёт счёт и период
    /// с главного экрана и открывает шкалу заново.
    /// </summary>
    public void Enter()
    {
        _initialized = true;

        var state = _stateManager.GetMainState();
        _syncedPeriodFrom = state.PeriodFrom;
        _syncedPeriodTo = state.PeriodTo;

        LoadScopes(state.SelectedAccountId);
        Reload(resetViewport: true);
    }

    /// <summary>Полная перезагрузка (смена базы данных).</summary>
    public void Reload() => Enter();

    public void SetPlotWidth(double width)
    {
        if (width <= 1d || Math.Abs(width - _plotWidth) < 1d)
        {
            return;
        }

        _plotWidth = width;

        if (_initialized)
        {
            RebuildBuckets();
        }
    }

    public void PanByPixels(double deltaX, double plotWidth)
    {
        if (!_initialized || plotWidth <= 1d || deltaX == 0d)
        {
            return;
        }

        var span = _viewTo - _viewFrom;
        var shiftTicks = (long)(-deltaX / plotWidth * span.Ticks);
        shiftTicks = Math.Clamp(shiftTicks, -span.Ticks, span.Ticks);

        if (shiftTicks == 0)
        {
            return;
        }

        SetViewport(_viewFrom.AddTicks(shiftTicks), _viewTo.AddTicks(shiftTicks));
    }

    public void ZoomAt(double anchorFraction, double factor)
    {
        if (!_initialized || factor <= 0d)
        {
            return;
        }

        anchorFraction = Math.Clamp(anchorFraction, 0d, 1d);

        var span = _viewTo - _viewFrom;
        var totalTicks = (_boundsTo - _boundsFrom).Ticks;
        var newTicks = Math.Clamp(
            (long)(span.Ticks / factor),
            TimeSpan.FromDays(MinZoomDays).Ticks,
            Math.Max(TimeSpan.FromDays(MinZoomDays).Ticks, totalTicks));

        var anchorTicks = _viewFrom.Ticks + (long)(span.Ticks * anchorFraction);
        var fromTicks = anchorTicks - (long)(newTicks * anchorFraction);

        SetViewport(FromTicks(fromTicks), FromTicks(fromTicks + newTicks));
    }

    public void ResetToPeriod()
    {
        if (!_initialized)
        {
            return;
        }

        ResetViewportToPeriod();
        RebuildBuckets();
    }

    [RelayCommand]
    public void ZoomIn() => ZoomAt(0.5d, ZoomStep);

    [RelayCommand]
    public void ZoomOut() => ZoomAt(0.5d, 1d / ZoomStep);

    partial void OnSelectedScopeChanged(TimelineScope? value)
    {
        if (_suppressScopeChange || value == null || !_initialized)
        {
            return;
        }

        Reload(resetViewport: false);
    }

    /// <summary>
    /// Пересобирает серию по выбранной области. Запросы идут на UI-потоке: LiteDB открывает
    /// файл эксклюзивно, и параллельное обращение из фонового потока ломает остальные запросы.
    /// </summary>
    private void Reload(bool resetViewport)
    {
        var version = ++_loadVersion;
        var scopeId = SelectedScope?.AccountId;

        IsLoading = true;

        try
        {
            var accounts = _repository.GetAccounts().ToList();
            var accountIds = TimelineScopeResolver.Resolve(accounts, scopeId);

            var series = accountIds.Count == 0
                ? DailySeries.Empty
                : TimelineSeriesBuilder.Build(
                    _repository.GetTransactions(new TransactionsFilter
                    {
                        PeriodFrom = HistoryFrom,
                        PeriodTo = HistoryTo,
                    }),
                    accountIds.ToHashSet());

            if (version != _loadVersion)
            {
                return;
            }

            _series = series;

            if (resetViewport)
            {
                ResetViewportToPeriod();
            }
            else
            {
                // Данные другой области — границы прокрутки могли сузиться.
                SetBounds(_highlightFrom, _highlightTo);
                ClampViewport();
            }

            RebuildBuckets();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить операции для таймлайна");

            if (version == _loadVersion)
            {
                _series = DailySeries.Empty;
                RenderModel = null;
                EndBalanceText = string.Empty;
                EmptyText = "Не удалось загрузить операции";
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    private void LoadScopes(Guid? preferredAccountId)
    {
        var total = _accounts.Total;
        if (total.Children.Count == 0)
        {
            _accounts.LoadAccounts();
        }

        _suppressScopeChange = true;
        try
        {
            Scopes.Clear();
            Scopes.Add(new TimelineScope(null, total.Name, 0, false));

            foreach (var account in total.Children)
            {
                Scopes.Add(new TimelineScope(account.Id, account.Name, 0, account.IsGroup));

                foreach (var child in account.Children)
                {
                    Scopes.Add(new TimelineScope(child.Id, child.Name, 1, child.IsGroup));
                }
            }

            SelectedScope = Scopes.FirstOrDefault(s => s.AccountId == preferredAccountId) ?? Scopes[0];
        }
        finally
        {
            _suppressScopeChange = false;
        }
    }

    private void ResetViewportToPeriod()
    {
        var state = _stateManager.GetMainState();

        var from = ClampPeriodStart(state.PeriodFrom);
        var to = ClampPeriodEnd(state.PeriodTo);
        if (to.Date < from.Date)
        {
            to = from;
        }

        _highlightFrom = from;
        _highlightTo = to;

        var days = (to.Date - from.Date).Days + 1;
        if (days < MinStartWindowDays)
        {
            var extra = MinStartWindowDays - days;
            from = from.AddDays(-(extra / 2));
            to = to.AddDays(extra - (extra / 2));
        }

        SetBounds(_highlightFrom, _highlightTo);

        _viewFrom = from;
        _viewTo = to;
        ClampViewport();
    }

    private DateTime ClampPeriodStart(DateTime value)
    {
        if (value.Year >= 1900)
        {
            return value;
        }

        return _series.IsEmpty ? DateTime.Today.AddYears(-1) : _series.FirstDay;
    }

    private DateTime ClampPeriodEnd(DateTime value)
    {
        if (value.Year <= HistoryTo.Year)
        {
            return value;
        }

        return _series.IsEmpty ? DateTime.Today : _series.LastDay;
    }

    private void SetBounds(DateTime periodFrom, DateTime periodTo)
    {
        var span = periodTo - periodFrom;
        if (span < TimeSpan.FromDays(MinStartWindowDays))
        {
            span = TimeSpan.FromDays(MinStartWindowDays);
        }

        var dataFrom = _series.IsEmpty ? periodFrom : _series.FirstDay;
        var dataTo = _series.IsEmpty ? periodTo : _series.LastDay;

        var min = dataFrom < periodFrom ? dataFrom : periodFrom;
        var max = dataTo > periodTo ? dataTo : periodTo;

        _boundsFrom = min - span;
        _boundsTo = max + span;
    }

    private void ClampViewport()
    {
        var total = _boundsTo - _boundsFrom;
        var span = _viewTo - _viewFrom;

        if (span >= total)
        {
            _viewFrom = _boundsFrom;
            _viewTo = _boundsTo;
            return;
        }

        if (_viewFrom < _boundsFrom)
        {
            _viewTo += _boundsFrom - _viewFrom;
            _viewFrom = _boundsFrom;
        }

        if (_viewTo > _boundsTo)
        {
            _viewFrom -= _viewTo - _boundsTo;
            _viewTo = _boundsTo;
        }
    }

    private void SetViewport(DateTime from, DateTime to)
    {
        if (to <= from)
        {
            return;
        }

        _viewFrom = from;
        _viewTo = to;
        ClampViewport();
        RebuildBuckets();
    }

    private void RebuildBuckets()
    {
        if (_series.IsEmpty || _viewTo <= _viewFrom)
        {
            RenderModel = null;
            EndBalanceText = string.Empty;
            EmptyText = _series.IsEmpty ? "По счёту ещё нет операций" : null;
            return;
        }

        var scale = TimelineBuckets.ChooseScale(_viewFrom, _viewTo, _plotWidth, RenderModel?.Scale);
        var buckets = TimelineBuckets.Build(_series, _viewFrom, _viewTo, scale);

        if (buckets.Count == 0)
        {
            RenderModel = null;
            EmptyText = "Нет операций за этот период";
            return;
        }

        var captions = TimelineBuckets.BuildCaptions(buckets, scale);
        var (floor, ceiling, balanceTicks) = BuildBalanceScale(
            buckets.Min(b => b.Balance),
            buckets.Max(b => b.Balance));

        var flowMax = BuildFlowMax(buckets);

        RenderModel = new TimelineRenderModel(
            buckets,
            captions,
            scale,
            _viewFrom,
            _viewTo,
            floor,
            ceiling,
            balanceTicks,
            flowMax,
            flowMax <= 0m ? [0m] : [-flowMax, 0m, flowMax],
            _highlightFrom,
            _highlightTo);

        EndBalanceText = MoneyFormat.Full(buckets[^1].Balance);
        EmptyText = buckets.All(b => b.Count == 0) ? "Нет операций за этот период" : null;
    }

    private static (decimal Floor, decimal Ceiling, IReadOnlyList<decimal> Ticks) BuildBalanceScale(decimal min, decimal max)
    {
        if (max <= min)
        {
            max = min + Math.Max(1m, Math.Abs(min) * 0.1m);
        }

        var range = max - min;
        decimal floor;
        decimal ceiling;
        decimal step;

        if (min >= 0m && min <= 0.35m * max)
        {
            // Баланс и так близко к нулю — столбцы честно растут от нуля.
            step = NiceTicks.Step(max, 4);
            floor = 0m;
            ceiling = NiceTicks.RoundUp(max + (range * 0.08m), step);
        }
        else
        {
            // Иначе шкала не от нуля: рост иначе не виден, но низ всегда подписан.
            step = NiceTicks.Step(range, 4);
            floor = NiceTicks.RoundDown(min - (range * 0.08m), step);
            ceiling = NiceTicks.RoundUp(max + (range * 0.08m), step);
        }

        var guard = 0;
        while ((ceiling - floor) / step > 8m && guard++ < 8)
        {
            step *= 2m;
            floor = NiceTicks.RoundDown(floor, step);
            ceiling = NiceTicks.RoundUp(ceiling, step);
        }

        if (ceiling <= floor)
        {
            ceiling = floor + step;
        }

        var ticks = new List<decimal>();
        for (var value = floor; value <= ceiling; value += step)
        {
            ticks.Add(value);
        }

        return (floor, ceiling, ticks);
    }

    private static decimal BuildFlowMax(IReadOnlyList<TimelineBucket> buckets)
    {
        var raw = buckets.Max(b => Math.Max(b.Income, b.Expense));
        if (raw <= 0m)
        {
            // Операций в окне нет — подписываем только нулевую линию.
            return 0m;
        }

        return NiceTicks.RoundUp(raw, NiceTicks.Step(raw, 3));
    }

    private static DateTime FromTicks(long ticks) =>
        new(Math.Clamp(ticks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks));

    private void SubscribeMessages()
    {
        WeakReferenceMessenger.Default.Register<TimelineViewModel, MainStateChangedMessage>(this, (vm, message) =>
        {
            if (message?.State == null || !vm._initialized)
            {
                return;
            }

            var state = message.State;
            if (state.PeriodFrom == vm._syncedPeriodFrom && state.PeriodTo == vm._syncedPeriodTo)
            {
                return;
            }

            vm._syncedPeriodFrom = state.PeriodFrom;
            vm._syncedPeriodTo = state.PeriodTo;

            vm.ResetViewportToPeriod();
            vm.RebuildBuckets();
        });

        WeakReferenceMessenger.Default.Register<TimelineViewModel, TransactionChangedMessage>(this, (vm, message) =>
        {
            if (vm._initialized)
            {
                vm.Reload(resetViewport: false);
            }
        });
    }
}
