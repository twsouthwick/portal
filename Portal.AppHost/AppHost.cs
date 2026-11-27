var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithRealmImport("./Realms");

builder.AddProject<Projects.Portal_Web>("media", launchProfileName: "media")
    .WithExternalHttpEndpoints()
    .AddPortalDefaults()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpHealthCheck("/health")
    .WaitFor(keycloak);

builder.AddProject<Projects.Portal_Web>("admin", launchProfileName: "admin")
    .WithExternalHttpEndpoints()
    .AddPortalDefaults()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpHealthCheck("/health")
    .WaitFor(keycloak);


builder.Build().Run();

static class PortalAppExtensions
{
    public static IResourceBuilder<T> AddPortalDefaults<T>(this IResourceBuilder<T> builder)
        where T : IResource, IResourceWithEndpoints, IResourceWithEnvironment
    {
        return builder
            .WithEnvironment("Oidc__Authority", "https://localhost:8080/realms/portal")
            .WithEnvironment("Oidc__ClientId", "portal-web")
            .WithEnvironment("Oidc__ClientSecret", "portal-development-secret");
    }
}