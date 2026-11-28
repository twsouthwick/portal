namespace Portal.Web.Services;

public sealed record NativeOidcMetadata(
    string Issuer,
    string ClientId,
    string ApiAudience,
    IReadOnlyList<string> Scopes);

public static class NativeOidcMetadataEndpointExtensions
{
    public static void MapNativeOidcMetadataEndpoint(
        this IEndpointRouteBuilder endpoints,
        IConfiguration configuration)
    {
        var oidc = configuration.GetSection("Oidc");
        var nativeScopes = oidc["NativeScopes"];
        string[] scopes = string.IsNullOrWhiteSpace(nativeScopes)
            ? ["openid", "groups"]
            : nativeScopes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var metadata = new NativeOidcMetadata(
            oidc["Authority"] ?? throw new InvalidOperationException("Oidc:Authority is required."),
            oidc["NativeClientId"] ?? string.Empty,
            oidc["ApiAudience"] ?? "portal-api",
            scopes);

        // @spec AUTH-020 AUTH-021 AUTH-022
        endpoints.MapGet("/.well-known/portal-native-oidc", (HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(metadata.ClientId))
            {
                return Results.NotFound();
            }

            context.Response.Headers.CacheControl = "no-cache";
            return Results.Json(metadata);
        })
        .AllowAnonymous()
        .WithName("GetNativeOidcMetadata")
        .WithSummary("Get public OIDC configuration for the native client.")
        .Produces<NativeOidcMetadata>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
