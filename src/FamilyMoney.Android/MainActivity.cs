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
    private const int LocalNetworkRequestCode = 4102;
    private const string LocalNetworkPermission = "android.permission.ACCESS_LOCAL_NETWORK";

    private TaskCompletionSource<bool>? _recordAudioPermissionTcs;
    private TaskCompletionSource<bool>? _localNetworkPermissionTcs;

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

    public Task<bool> RequestLocalNetworkPermissionAsync()
    {
        // Разрешение появилось в Android 17 (API 37). На более старых
        // версиях локальная сеть доступна с обычным INTERNET.
        if ((int)Build.VERSION.SdkInt < 37)
        {
            return Task.FromResult(true);
        }

        if (ContextCompat.CheckSelfPermission(this, LocalNetworkPermission) == Permission.Granted)
        {
            return Task.FromResult(true);
        }

        _localNetworkPermissionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ActivityCompat.RequestPermissions(this, [LocalNetworkPermission], LocalNetworkRequestCode);
        return _localNetworkPermissionTcs.Task;
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == RecordAudioRequestCode)
        {
            var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            _recordAudioPermissionTcs?.TrySetResult(granted);
            _recordAudioPermissionTcs = null;
            return;
        }

        if (requestCode == LocalNetworkRequestCode)
        {
            var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            _localNetworkPermissionTcs?.TrySetResult(granted);
            _localNetworkPermissionTcs = null;
        }
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
