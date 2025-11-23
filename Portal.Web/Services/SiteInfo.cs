using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;

namespace Portal.Web.Services;



/// <summary>
/// Represents a store link for an app
/// </summary>
public record AppStore
{
    /// <summary>
    /// Store name (e.g., "google-play", "apple")
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Store URL or ID
    /// </summary>
    public string Id { get; init; } = string.Empty;
}

/// <summary>
/// Represents an app that can be launched from a site
/// </summary>
public record SiteApp
{
    /// <summary>
    /// App name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// App scheme/protocol (e.g., "audiobookshelf://", "plappa://")
    /// </summary>
    public string Scheme { get; init; } = string.Empty;

    /// <summary>
    /// Store links for downloading the app
    /// </summary>
    public IReadOnlyCollection<AppStore> Stores { get; init; } = [];
}

/// <summary>
/// Represents information about a subsite in the intranet portal
/// </summary>
public record SiteInfo
{
    /// <summary>
    /// Unique identifier for the subsite
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Display name of the subsite
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Brief description of the subsite
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Primary URL for the subsite
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Apps that can be launched for this subsite
    /// </summary>
    public IReadOnlyCollection<SiteApp> Apps { get; init; } = [];

    /// <summary>
    /// Icon class (e.g., Bootstrap icons, Font Awesome)
    /// </summary>
    public string Icon { get; init; } = "bi bi-globe";

    /// <summary>
    /// Categories or departments the subsite belongs to
    /// </summary>
    public IReadOnlyCollection<string> Categories { get; init; } = [];

    /// <summary>
    /// Detailed usage instructions
    /// </summary>
    public string UsageInstructions { get; init; } = string.Empty;

    [JsonIgnore]
    public IFileProvider? Content { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = [];

    public bool IsPrivate { get; init; }

    public bool IsInternal { get; init; }
}
