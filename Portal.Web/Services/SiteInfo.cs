using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;

namespace Portal.Web.Services;

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
    /// URL to navigate to the subsite
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// App protocol URL for launching native application (e.g., ms-teams://, slack://)
    /// </summary>
    public string? AppUrl { get; set; }

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
