using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Portal.Web.Services;

public static class SiteDetailsExtensions
{
    public static void AddPortalProvider(this IHostApplicationBuilder host)
    {
        var files = host.GetFileProvider();

        host.Services.AddSingleton<IPortalProvider>(new FilePortalProvider(files));
    }

    private static IFileProvider GetFileProvider(this IHostApplicationBuilder host)
    {
        if (host.Configuration["SITE_PATH"] is { } path)
        {
            if (File.Exists(path) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return new ZipFileProvider(path);
            }
            else if (Directory.Exists(path))
            {
                return new PhysicalFileProvider(path);
            }
            else
            {
                throw new FileNotFoundException($"SITE_PATH '{path}' does not exist or is not a valid ZIP file or directory.");
            }
        }
        else
        {
            return host.Environment.ContentRootFileProvider.GetSubdirectory("sites");
        }
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
        var portal = endpoints.ServiceProvider.GetRequiredService<IPortalProvider>();
        var siteDetails =  portal.GetSiteDetailsAsync(default).GetAwaiter().GetResult();

        foreach (var site in siteDetails.Sites)
        {
            var group = endpoints.MapGroup($"/{site.Id}/content");

            // Apply authorization if roles are specified
            if (site.Roles.Count > 0)
            {
                group.RequireAuthorization(policy =>
                {
                    policy.RequireAssertion(context =>
                        site.IsAuthorized(context.User));
                });
            }

            // Map the file serving endpoint using Results.File
            group.MapGet("/{**path}", (string path) =>
            {
                if (site.Content?.GetFileInfo(path) is not { Exists: true, IsDirectory: false } fileInfo)
                {
                    return Results.NotFound();
                }

                var contentType = GetContentType(fileInfo.Name);

                return Results.File(
                    fileInfo.CreateReadStream(),
                    contentType,
                    fileInfo.Name,
                    fileInfo.LastModified,
                    enableRangeProcessing: true);
            })
            .WithName($"SiteContent_{site.Id}")
            .WithDisplayName($"{site.Name} Content")
            .DisableAntiforgery();
        }
    }

    private static string GetContentType(string fileName)
    {
        var provider = new FileExtensionContentTypeProvider();
        return provider.TryGetContentType(fileName, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}
