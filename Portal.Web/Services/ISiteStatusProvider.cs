namespace Portal.Web.Services;

public enum SiteStatus
{
    Available,
    Unavailable
}

public interface ISiteStatusProvider<in TSite>
{
    ValueTask<SiteStatus> GetSiteStatusAsync(TSite site, CancellationToken cancellationToken = default);
}
