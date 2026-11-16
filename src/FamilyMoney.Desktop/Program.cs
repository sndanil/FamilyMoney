using Avalonia;
using Avalonia.X11;
using System;
using System.Threading;

namespace FamilyMoney;

internal sealed class Program
{
    // Именованный мьютекс живёт всё время работы приложения: второй запуск
    // не должен открывать ту же базу LiteDB отдельным процессом.
    private static Mutex? _instanceMutex;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (!TryAcquireSingleInstance())
        {
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static bool TryAcquireSingleInstance()
    {
        try
        {
            _instanceMutex = new Mutex(true, "FamilyMoney.Desktop.SingleInstance", out var createdNew);
            return createdNew;
        }
        catch (Exception)
        {
            // Именованный мьютекс недоступен в этом окружении — работаем без защиты,
            // это лучше, чем не запуститься совсем.
            _instanceMutex = null;
            return true;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // WM_CLASS для Linux: по нему .desktop-файл находит иконку и группирует окно.
            .With(new X11PlatformOptions { WmClass = "familymoney" })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
