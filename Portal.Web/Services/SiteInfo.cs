using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;

namespace Portal.Web.Services;



/// <summary>
/// Represents a store link for an app
/// </summary>
public class AppStore
{
    /// <summary>
    /// Store name (e.g., "google-play", "apple")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Store URL or ID
    /// </summary>
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Represents an app that can be launched from a site
/// </summary>
public class SiteApp
{
    /// <summary>
    /// App name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// App scheme/protocol (e.g., "audiobookshelf://", "plappa://")
    /// </summary>
    public string Scheme { get; set; } = string.Empty;

    /// <summary>
    /// Store links for downloading the app
    /// </summary>
    public IReadOnlyCollection<AppStore> Store { get; set; } = [];
}

/// <summary>
/// Represents information about a subsite in the intranet portal
/// </summary>
public class SiteInfo
{
    /// <summary>
    /// Unique identifier for the subsite
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the subsite
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Brief description of the subsite
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Primary URL for the subsite
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Apps that can be launched for this subsite
    /// </summary>
    public IReadOnlyCollection<SiteApp> Apps { get; set; } = [];

    /// <summary>
    /// Icon class (e.g., Bootstrap icons, Font Awesome)
    /// </summary>
    public string Icon { get; set; } = "bi bi-globe";

    /// <summary>
    /// Categories or departments the subsite belongs to
    /// </summary>
    public IReadOnlyCollection<string> Categories { get; set; } = [];

    /// <summary>
    /// Detailed usage instructions
    /// </summary>
    public string UsageInstructions { get; set; } = string.Empty;

    [JsonIgnore]
    public IFileProvider? Content { get; set; }

    public IReadOnlyCollection<string> Roles { get; set; } = [];

    public bool IsPrivate { get; set; }

    public bool IsInternal { get; set; }
}
