namespace Portal.Web.Services;

public interface IPortalProvider
{
    ValueTask<PortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default);
}