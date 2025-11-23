using System.Text.Json.Serialization;

namespace Portal.Web.Browser;

public enum BrowserPlatform
{
    Unknown,
    Windows,
    MacOS,
    Linux,
    iOS,
    Android,
}

/// <summary>
/// Information about the client's browser and platform
/// </summary>
public sealed record BrowserInfo
{
    /// <summary>
    /// Browser name (e.g., "Chrome", "Edge", "Firefox", "Safari")
    /// </summary>
    public required string Browser { get; init; }

    /// <summary>
    /// Browser version
    /// </summary>
    public required string BrowserVersion { get; init; }

    /// <summary>
    /// Operating system (e.g., "Windows", "macOS", "Linux", "iOS", "Android")
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<BrowserPlatform>))]
    public BrowserPlatform Platform { get; init; }

    /// <summary>
    /// Platform version
    /// </summary>
    public required string PlatformVersion { get; init; }

    /// <summary>
    /// Whether the device is mobile
    /// </summary>
    public required bool IsMobile { get; init; }

    /// <summary>
    /// CPU architecture (e.g., "x86", "arm")
    /// </summary>
    public required string Architecture { get; init; }

    /// <summary>
    /// Whether the browser is in private/incognito mode
    /// </summary>
    public required bool IsPrivate { get; init; }
}
