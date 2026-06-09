using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace FamilyMoney.Android;

[Activity(
    Label = "Семейные деньги",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
