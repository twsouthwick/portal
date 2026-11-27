using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Portal.Web.Services;

public static class ClientManifestEndpointExtensions
{
    public const string AuthenticationScheme = "ClientManifestBearer";
    public const string AuthorizationPolicy = "ClientManifest";

    public static void MapClientManifestEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/manifest", async (
            HttpContext context,
            IPortalProvider portal,
            IOptions<HttpJsonOptions> jsonOptions,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger(nameof(ClientManifestEndpointExtensions));
            PortalDetails details;
            try
            {
                details = await portal.GetSiteDetailsAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to create the client manifest.");
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "The client manifest is temporarily unavailable.");
            }

            var manifest = ClientManifest.Create(details, context.User);
            var body = JsonSerializer.SerializeToUtf8Bytes(
                manifest,
                jsonOptions.Value.SerializerOptions);
            var etag = new EntityTagHeaderValue(
                $"\"{Convert.ToHexString(SHA256.HashData(body))}\"");

            context.Response.Headers.CacheControl = "private, no-cache";
            context.Response.Headers.Vary = "Authorization, Cookie";
            context.Response.Headers.ETag = etag.ToString();

            var ifNoneMatch = context.Request.GetTypedHeaders().IfNoneMatch;
            if (ifNoneMatch?.Any(candidate =>
                candidate.Tag == "*" ||
                string.Equals(candidate.Tag.ToString(), etag.Tag.ToString(), StringComparison.Ordinal)) == true)
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            return Results.Bytes(body, "application/json");
        })
        .RequireAuthorization(AuthorizationPolicy)
        .WithName("GetClientManifest")
        .WithSummary("Get the current user's client manifest.")
        .WithDescription("Returns the role-filtered client manifest for the authenticated user.")
        .Produces<ClientManifest>()
        .Produces(StatusCodes.Status304NotModified)
        .Produces<ProblemDetails>(
            StatusCodes.Status401Unauthorized,
            contentType: "application/problem+json")
        .Produces<ProblemDetails>(
            StatusCodes.Status503ServiceUnavailable,
            contentType: "application/problem+json");
    }
}
