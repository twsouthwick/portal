using Microsoft.JSInterop;

namespace Portal.Web.Browser;

/// <summary>
/// JavaScript interop implementation of client browser session
/// </summary>
internal sealed class JsClientBrowserSession : IClientBrowserSession, IAsyncDisposable
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

    public async Task<bool> IsPrivateModeAsync()
    {
        try
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<bool>("isPrivateMode");
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "JavaScript error detecting private mode");
            return false;
        }
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
                Platform = "Unknown",
                PlatformVersion = "",
                IsMobile = false,
                Architecture = ""
            };
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
