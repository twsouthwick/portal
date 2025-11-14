using System.Security.Claims;

namespace Portal.Web.Services;

/// <summary>
/// Service for managing subsite information
/// </summary>
public interface ISiteDetails
{
    string Name { get; }

    string Description { get; }

    IEnumerable<SubsiteInfo> GetSites(ClaimsPrincipal user);

    IEnumerable<SubsiteInfo> Sites { get; }
}
