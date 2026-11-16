using Avalonia;
using System;
using System.Threading;

namespace FamilyMoney.Navigation.Desktop
{
    internal sealed class Program
    {
        // Второй запуск того же приложения не должен открывать базу повторно.
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
                _instanceMutex = new Mutex(true, "FamilyMoney.Navigation.Desktop.SingleInstance", out var createdNew);
                return createdNew;
            }
            catch (Exception)
            {
                // Именованный мьютекс недоступен в этом окружении — работаем без защиты.
                _instanceMutex = null;
                return true;
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
#if DEBUG
                .WithDeveloperTools()
#endif
                .WithInterFont()
                .LogToTrace();
    }
}
