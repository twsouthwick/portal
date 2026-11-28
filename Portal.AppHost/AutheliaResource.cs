using Aspire.Hosting.ApplicationModel;

sealed class AutheliaResource(string name) : ContainerResource(name)
{
    public const string Issuer = "https://auth.dev.localhost:8080";
}

static class AutheliaExtensions
{
    // @spec RUNTIME-001
    public static IResourceBuilder<AutheliaResource> AddAuthelia(this IDistributedApplicationBuilder builder, string name)
    {
        var secretsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aspnet", "portal-authelia");
        var sessionSecret = builder.AddParameter("authelia-session-secret", secret: true);
        var storageKey = builder.AddParameter("authelia-storage-key", secret: true);
        var hmacSecret = builder.AddParameter("authelia-oidc-hmac-secret", secret: true);

        return builder.AddResource(new AutheliaResource(name))
            .WithImage("authelia/authelia", "4.39.28")
            .WithHttpsEndpoint(port: 8080, targetPort: 9091, name: "https")
            .WithBindMount("./Authelia/configuration.yml", "/config/configuration.yml", isReadOnly: true)
            .WithBindMount("./Authelia/users.yml", "/config/users.yml", isReadOnly: true)
            .WithBindMount(secretsDirectory, "/secrets", isReadOnly: true)
            .WithVolume("portal-authelia-data", "/data")
            .WithEntrypoint("authelia")
            .WithArgs("--config", "/config/configuration.yml", "--config.experimental.filters", "template")
            .WithContainerRuntimeArgs("--no-healthcheck")
            .WithEnvironment("AUTHELIA_SESSION_SECRET", sessionSecret)
            .WithEnvironment("AUTHELIA_STORAGE_ENCRYPTION_KEY", storageKey)
            .WithEnvironment("AUTHELIA_IDENTITY_PROVIDERS_OIDC_HMAC_SECRET", hmacSecret)
            .WithHttpHealthCheck("/api/health", endpointName: "https");
    }
}
