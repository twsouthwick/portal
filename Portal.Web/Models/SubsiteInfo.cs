namespace Portal.Web.Models;

/// <summary>
/// Represents information about a subsite in the intranet portal
/// </summary>
public class SubsiteInfo
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
    /// Icon class (e.g., Bootstrap icons, Font Awesome)
    /// </summary>
    public string IconClass { get; set; } = "bi bi-globe";

    /// <summary>
    /// Category or department the subsite belongs to
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Detailed usage instructions
    /// </summary>
    public string UsageInstructions { get; set; } = string.Empty;

    /// <summary>
    /// Key features of the subsite
    /// </summary>
    public List<string> Features { get; set; } = new();

    /// <summary>
    /// Contact person or team for the subsite
    /// </summary>
    public string Contact { get; set; } = string.Empty;

    /// <summary>
    /// Whether the subsite requires special permissions
    /// </summary>
    public bool RequiresPermission { get; set; } = false;

    /// <summary>
    /// Target audience for the subsite
    /// </summary>
    public string TargetAudience { get; set; } = string.Empty;
}
