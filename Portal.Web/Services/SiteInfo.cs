using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;

namespace Portal.Web.Services;

public enum AppStore
{
    None,
    Apple,
    MacOS,
    Google,
    Microsoft,
    Windows,
    Linux,
}

/// <summary>
/// Represents an app that can be launched from a site
/// </summary>
public record SiteApp
{
    /// <summary>
    /// App name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// App scheme/protocol (e.g., "audiobookshelf://", "plappa://")
    /// </summary>
    public string Scheme { get; init; } = null!;

    /// <summary>
    /// Store name (e.g., "google-play", "apple")
    /// </summary>
    public AppStore Store { get; init; }

    /// <summary>
    /// Store URL or ID
    /// </summary>
    public required string StoreId { get; init; }
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
    public IReadOnlyCollection<SiteApp> Apps
    {
        get => field ?? [];
        init => field = value ?? [];
    }

    /// <summary>
    /// Icon class (e.g., Bootstrap icons, Font Awesome)
    /// </summary>
    public string Icon { get; init; } = "bi bi-globe";

    /// <summary>
    /// Categories or departments the subsite belongs to
    /// </summary>
    public IReadOnlyCollection<string> Categories
    {
        get => field ?? [];
        init => field = value ?? [];
    }

    /// <summary>
    /// Detailed usage instructions
    /// </summary>
    public string UsageInstructions { get; init; } = string.Empty;

    [JsonIgnore]
    public IFileProvider? Content { get; init; }

    public IReadOnlyCollection<string> Roles
    {
        get => field ?? [];
        init => field = value ?? [];
    }

    public bool IsPrivate { get; init; }

    public bool IsInternal { get; init; }
}
