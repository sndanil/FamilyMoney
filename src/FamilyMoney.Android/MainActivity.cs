using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
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
    private const int RecordAudioRequestCode = 4101;

    private TaskCompletionSource<bool>? _recordAudioPermissionTcs;

    public static MainActivity? Instance { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Instance = this;
        base.OnCreate(savedInstanceState);
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }

        base.OnDestroy();
    }

    public Task<bool> RequestRecordAudioPermissionAsync()
    {
        if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.RecordAudio) == Permission.Granted)
        {
            return Task.FromResult(true);
        }

        _recordAudioPermissionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ActivityCompat.RequestPermissions(this, [Manifest.Permission.RecordAudio], RecordAudioRequestCode);
        return _recordAudioPermissionTcs.Task;
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode != RecordAudioRequestCode)
        {
            return;
        }

        var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
        _recordAudioPermissionTcs?.TrySetResult(granted);
        _recordAudioPermissionTcs = null;
    }

    public Task RunOnUiThreadAsync(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RunOnUiThread(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }
}
