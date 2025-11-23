using Microsoft.JSInterop;

namespace Portal.Web.Browser;

/// <summary>
/// JavaScript interop implementation of client browser session
/// </summary>
internal sealed class JsClientBrowserSession : IClientBrowserSession, ILocalStorageProvider, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<JsClientBrowserSession> _logger;
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public JsClientBrowserSession(IJSRuntime jsRuntime, ILogger<JsClientBrowserSession> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _moduleTask = new Lazy<Task<IJSObjectReference>>(() =>
            _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./dist/app.js").AsTask());
    }

    public async Task OpenUrlInNewTabAsync(string url)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("openUrlInNewTab", url);
    }

    public async Task<bool> LaunchAppUrlAsync(string url)
    {
        try
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<bool>("launchAppUrl", url);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "JavaScript error launching app URL: {Url}", url);
            return false;
        }
    }

    public async Task<BrowserInfo> GetBrowserInfoAsync()
    {
        try
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<BrowserInfo>("getBrowserInfo");
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "JavaScript error getting browser info");
            // Return a default/unknown browser info on error
            return new BrowserInfo
            {
                Browser = "Unknown",
                BrowserVersion = "",
                Platform = BrowserPlatform.Unknown,
                PlatformVersion = "",
                IsMobile = false,
                Architecture = "",
                IsPrivate = false
            };
        }
    }

    // ILocalStorageProvider implementation
    public async Task<T?> GetItemAsync<T>(string key)
    {
        try
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<T?>("getItem", key);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "JavaScript error getting item from localStorage: {Key}", key);
            return default;
        }
    }

    public async Task SetItemAsync<T>(string key, T value)
    {
        try
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("setItem", key, value);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "JavaScript error setting item in localStorage: {Key}", key);
        }
    }

    public async Task RemoveItemAsync(string key)
    {
        try
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("removeItem", key);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "JavaScript error removing item from localStorage: {Key}", key);
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("clear");
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "JavaScript error clearing localStorage");
        }
    }

    public async Task<string[]> GetKeysAsync()
    {
        try
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<string[]>("getKeys");
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "JavaScript error getting keys from localStorage");
            return [];
        }
    }

    public async Task<bool> HasKeyAsync(string key)
    {
        try
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<bool>("hasKey", key);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "JavaScript error checking key in localStorage: {Key}", key);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            try
            {
                var module = await _moduleTask.Value;
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // This happens a lot, so we won't log it
            }
        }
    }
}
