var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Portal_Web>("media")
    .WithHttpEndpoint(6110)
    .WithHttpsEndpoint(6111)
    .WithExternalHttpEndpoints()
    .AddPortalDefaults("testuser1", "media")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Portal_Web>("admin")
    .WithHttpEndpoint(6000)
    .WithHttpsEndpoint(6001)
    .WithExternalHttpEndpoints()
    .AddPortalDefaults("testuser1", "admin", "media")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpHealthCheck("/health");


builder.Build().Run();

static class PortalAppExtensions
{
    public static IResourceBuilder<T> AddPortalDefaults<T>(this IResourceBuilder<T> builder, string name, params string[] roles)
        where T : IResource, IResourceWithEndpoints, IResourceWithEnvironment
    {
        return builder
            .WithEnvironment("TEST_USER", name)
            .WithEnvironment("TEST_ROLES", string.Join(",", roles));

    }
}