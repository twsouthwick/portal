using Microsoft.AspNetCore.StaticFiles;
using System.Security.Claims;

namespace Portal.Web.Services;

public static class SiteDetailsExtensions
{
    public static void AddPortalProvider(this IHostApplicationBuilder host, Action<PortalProviderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var portal = new PortalProviderBuilder(host);
        configure(portal);

        host.Services.AddSingleton<IPortalProvider>(services =>
        {
            var providers = new List<IPortalProvider>();
            foreach (var factory in portal.ProviderFactories)
            {
                if (factory(services) is { } provider)
                {
                    providers.Add(provider);
                }
            }

            if (providers.Count == 0 && portal.FallbackProviderFactory is { } fallbackFactory)
            {
                providers.Add(fallbackFactory(services));
            }

            return new CompositePortalProvider([.. providers]);
        });
    }

    public static bool IsAuthorized(this SiteInfo info, IReadOnlyCollection<string> roles)
    {
        if (info.Roles.Count == 0)
        {
            return true;
        }

        foreach (var role in info.Roles)
        {
            if (roles.Contains(role))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsAuthorized(this SiteInfo info, ClaimsPrincipal user)
    {
        if (info.Roles.Count == 0)
        {
            return true;
        }

        foreach (var role in info.Roles)
        {
            if (user.IsInRole(role))
            {
                return true;
            }
        }

        return false;
    }

    public static void MapSiteContentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{siteId}/content/{**path}", async (
            string siteId, string? path, HttpContext context, IPortalProvider portal, CancellationToken cancellationToken) =>
        {
            var site = await portal.GetSiteAsync(siteId, cancellationToken);
            if (site is null)
            {
                return Results.NotFound();
            }

            if (!site.IsAuthorized(context.User))
            {
                return Results.Forbid();
            }

            if (string.IsNullOrEmpty(path) || path.Split('/', '\\').Any(segment => segment is "." or "..") ||
                site.Content?.GetFileInfo(path) is not { Exists: true, IsDirectory: false } fileInfo)
            {
                return Results.NotFound();
            }

            DateTimeOffset? lastModified = fileInfo.LastModified == DateTimeOffset.MinValue ? null : fileInfo.LastModified;
            return Results.File(
                fileInfo.CreateReadStream(),
                GetContentType(fileInfo.Name),
                fileInfo.Name,
                lastModified,
                enableRangeProcessing: true);
        })
        .RequireAuthorization()
        .DisableAntiforgery();
    }

    private static string GetContentType(string fileName)
    {
        var provider = new FileExtensionContentTypeProvider();
        return provider.TryGetContentType(fileName, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}
