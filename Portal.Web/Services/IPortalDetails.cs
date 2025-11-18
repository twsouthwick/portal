namespace Portal.Web.Services;

/// <summary>
/// Service for managing subsite information
/// </summary>
public interface IPortalDetails
{
    string Name { get; }

    string Description { get; }

    IEnumerable<SiteInfo> Sites { get; }
}
