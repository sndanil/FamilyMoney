using FamilyMoney.Services;

namespace FamilyMoney.Android.Services;

public sealed class AndroidLocalNetworkPermissionService : ILocalNetworkPermissionService
{
    public Task<bool> EnsureGrantedAsync()
    {
        var activity = MainActivity.Instance;
        return activity == null
            ? Task.FromResult(false)
            : activity.RequestLocalNetworkPermissionAsync();
    }
}
