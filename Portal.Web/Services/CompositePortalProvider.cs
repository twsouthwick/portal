namespace Portal.Web.Services;

public sealed class CompositePortalProvider : IPortalProvider
{
    private readonly IReadOnlyList<IPortalProvider> _providers;

    public CompositePortalProvider(params IPortalProvider[] providers)
    {
        _providers = providers;
    }

    public async ValueTask<PortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default)
    {
        PortalDetails? portal = null;
        var sites = new List<SiteInfo>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in _providers)
        {
            var details = await provider.GetSiteDetailsAsync(cancellationToken);
            portal ??= details;
            foreach (var site in details.Sites)
            {
                if (ids.Add(site.Id))
                {
                    sites.Add(site);
                }
            }
        }

        return (portal ?? new PortalDetails("Portal")) with { Sites = sites };
    }

    public async ValueTask<SiteInfo?> GetSiteAsync(string siteId, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            if (await provider.GetSiteAsync(siteId, cancellationToken) is { } site)
            {
                return site;
            }
        }

        return null;
    }
}
