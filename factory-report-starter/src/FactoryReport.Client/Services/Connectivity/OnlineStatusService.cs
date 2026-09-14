using Microsoft.JSInterop;

namespace FactoryReport.Client.Services.Connectivity;

public interface IOnlineStatusService : IAsyncDisposable
{
    bool IsOnline { get; }
    event Action? ConnectivityChanged;
    Task InitializeAsync();
}

public sealed class OnlineStatusService(IJSRuntime jsRuntime) : IOnlineStatusService
{
    private DotNetObjectReference<OnlineStatusService>? _reference;
    private bool _initialized;

    public bool IsOnline { get; private set; } = true;

    public event Action? ConnectivityChanged;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _reference = DotNetObjectReference.Create(this);
        IsOnline = await jsRuntime.InvokeAsync<bool>("factoryReportPwa.getOnlineStatus").ConfigureAwait(false);
        await jsRuntime.InvokeVoidAsync("factoryReportPwa.registerOnlineHandlers", _reference).ConfigureAwait(false);
        _initialized = true;
    }

    [JSInvokable]
    public void OnOnlineChanged(bool isOnline)
    {
        IsOnline = isOnline;
        ConnectivityChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_reference is not null)
        {
            try
            {
                await jsRuntime.InvokeVoidAsync("factoryReportPwa.unregisterOnlineHandlers").ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // 页面卸载时忽略
            }

            _reference.Dispose();
        }
    }
}
