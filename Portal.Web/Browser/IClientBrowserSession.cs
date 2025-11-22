namespace Portal.Web.Browser;

/// <summary>
/// Service for client-side browser operations including URL launching and browser detection
/// </summary>
public interface IClientBrowserSession
{
    /// <summary>
    /// Detects if the browser is currently in private/incognito mode
    /// </summary>
    /// <returns>True if in private mode, false otherwise</returns>
    Task<bool> IsPrivateModeAsync();

    /// <summary>
    /// Opens a URL in a new browser tab
    /// </summary>
    /// <param name="url">The URL to open</param>
    Task OpenUrlInNewTabAsync(string url);

    /// <summary>
    /// Attempts to launch an app protocol URL and detects if it succeeded
    /// </summary>
    /// <param name="url">The app protocol URL to launch</param>
    /// <returns>True if the app likely launched, false otherwise</returns>
    Task<bool> LaunchAppUrlAsync(string url);
}
