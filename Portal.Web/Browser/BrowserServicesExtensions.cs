using Portal.Web.Browser;

namespace Portal.Web;

/// <summary>
/// Extension methods for registering client browser session services
/// </summary>
public static class ClientBrowserSessionExtensions
{
    /// <summary>
    /// Adds client browser session service to the service collection
    /// </summary>
    public static IServiceCollection AddClientBrowserSession(this IServiceCollection services)
    {
        services.AddScoped<JsClientBrowserSession>();
        services.AddScoped<IClientBrowserSession>(sp => sp.GetRequiredService<JsClientBrowserSession>());
        services.AddScoped<ILocalStorageProvider>(sp => sp.GetRequiredService<JsClientBrowserSession>());
        
        return services;
    }
}
