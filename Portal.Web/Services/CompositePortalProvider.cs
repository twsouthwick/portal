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
        foreach (var provider in _providers)
        {
            var details = await provider.GetSiteDetailsAsync(cancellationToken);
            if (details.Sites.Any())
            {
                return details;
            }
        }

        return new PortalDetails("Portal");
    }
}
