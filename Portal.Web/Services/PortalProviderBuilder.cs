namespace Portal.Web.Services;

public sealed class PortalProviderBuilder(IHostApplicationBuilder host)
{
    public IHostApplicationBuilder Host { get; } = host;

    internal List<Func<IServiceProvider, IPortalProvider?>> ProviderFactories { get; } = [];

    internal Func<IServiceProvider, IPortalProvider>? FallbackProviderFactory { get; private set; }

    public PortalProviderBuilder AddProvider(Func<IServiceProvider, IPortalProvider?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ProviderFactories.Add(factory);
        return this;
    }

    public PortalProviderBuilder AddFallbackProvider(Func<IServiceProvider, IPortalProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (FallbackProviderFactory is not null)
        {
            throw new InvalidOperationException("A fallback portal provider is already registered.");
        }

        FallbackProviderFactory = factory;
        return this;
    }
}
