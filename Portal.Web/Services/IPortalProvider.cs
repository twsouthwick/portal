namespace Portal.Web.Services;

public interface IPortalProvider
{
    ValueTask<PortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default);

    async ValueTask<SiteInfo?> GetSiteAsync(string siteId, CancellationToken cancellationToken = default)
    {
        var details = await GetSiteDetailsAsync(cancellationToken);
        return details.Sites.FirstOrDefault(site =>
            string.Equals(site.Id, siteId, StringComparison.OrdinalIgnoreCase));
    }
}