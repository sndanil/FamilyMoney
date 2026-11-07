using System;

namespace FamilyMoney.ViewModels;

/// <summary>
/// Раздел «Отчёты». Пока внутри один режим — «Таймлайн»; контейнер оставлен,
/// чтобы следующие отчёты добавлялись сюда же.
/// </summary>
public partial class ReportsViewModel : ViewModelBase
{
    public ReportsViewModel(TimelineViewModel timeline)
    {
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
    }

    public TimelineViewModel Timeline { get; }

    /// <summary>Вход в раздел: шкала открывается заново от периода главного экрана.</summary>
    public void Enter() => Timeline.Enter();

    /// <summary>Полная перезагрузка — например, после смены базы данных.</summary>
    public void Reload() => Timeline.Reload();
}
