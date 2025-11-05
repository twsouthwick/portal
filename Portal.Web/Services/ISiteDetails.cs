using System.Security.Claims;

namespace Portal.Web.Services;

/// <summary>
/// Service for managing subsite information
/// </summary>
public interface ISiteDetails
{
    string Name { get; }

    string Description { get; }

    IEnumerable<SubsiteInfo> GetSites(IReadOnlyCollection<string> roles);

    IEnumerable<SubsiteInfo> Sites { get; }
}
