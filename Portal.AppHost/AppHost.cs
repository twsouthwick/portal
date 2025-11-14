var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Portal_Web>("media", launchProfileName: "media")
    .WithExternalHttpEndpoints()
    .AddPortalDefaults("testuser1", "media")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Portal_Web>("admin", launchProfileName: "admin")
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