namespace Portal.Web.Services;

public interface IPortalProvider
{
    ValueTask<IPortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default);
}