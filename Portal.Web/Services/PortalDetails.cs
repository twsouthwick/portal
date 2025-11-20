namespace Portal.Web.Services;

/// <summary>
/// Service for managing subsite information
/// </summary>
public record PortalDetails(string Name)
{
    public string Description { get; init; } = string.Empty;

    public IEnumerable<SiteInfo> Sites { get; init; } = [];
}
