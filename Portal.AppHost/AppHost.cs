var builder = DistributedApplication.CreateBuilder(args);

// @spec RUNTIME-002
var clientSecret = builder.AddParameter("portal-client-secret", secret: true);
var authelia = builder.AddAuthelia("authelia");

builder.AddDotnetProject("media", "../Portal.Web/Portal.Web.csproj", options => options.LaunchProfileName = "media")
    .WithExternalHttpEndpoints()
    .AddPortalDefaults(clientSecret)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpHealthCheck("/health")
    .WaitFor(authelia);

builder.AddDotnetProject("admin", "../Portal.Web/Portal.Web.csproj", options => options.LaunchProfileName = "admin")
    .WithExternalHttpEndpoints()
    .AddPortalDefaults(clientSecret)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpHealthCheck("/health")
    .WaitFor(authelia);


builder.Build().Run();

static class PortalAppExtensions
{
    public static IResourceBuilder<T> AddPortalDefaults<T>(this IResourceBuilder<T> builder, IResourceBuilder<ParameterResource> clientSecret)
        where T : IResource, IResourceWithEndpoints, IResourceWithEnvironment
    {
        return builder
            .WithEnvironment("Oidc__Authority", AutheliaResource.Issuer)
            .WithEnvironment("Oidc__ClientId", "portal-web")
            .WithEnvironment("Oidc__ClientSecret", clientSecret);
    }
}