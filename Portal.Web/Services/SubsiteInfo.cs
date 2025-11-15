using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Portal.Web.Services;

public static class SubsiteParser
{
    public static async ValueTask AddSite(this IHostApplicationBuilder host)
    {
        if (host.Configuration["SITE_PATH"] is { } path)
        {
            if (File.Exists(path) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var zipProvider = new ZipFileProvider(path);
                host.Services.AddSingleton<ISiteDetails>(await CreateSite(zipProvider));
            }
            else if (Directory.Exists(path))
            {
                // Load from directory
                var physicalProvider = new PhysicalFileProvider(path);
                host.Services.AddSingleton<ISiteDetails>(await CreateSite(physicalProvider));
            }
            else
            {
                throw new FileNotFoundException($"SITE_PATH '{path}' does not exist or is not a valid ZIP file or directory.");
            }
        }
        else
        {
            // Default to sites subdirectory
            var fileProvider = host.Environment.ContentRootFileProvider.GetSubdirectory("sites");
            host.Services.AddSingleton<ISiteDetails>(await CreateSite(fileProvider));
        }
    }

    public static bool IsAuthorized(this SubsiteInfo info, IReadOnlyCollection<string> roles)
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

    public static bool IsAuthorized(this SubsiteInfo info, ClaimsPrincipal user)
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
        var siteDetails = endpoints.ServiceProvider.GetRequiredService<ISiteDetails>();

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

    public static async ValueTask<ISiteDetails> CreateSite(IFileProvider files)
    {
        var details = await PortalDetails.Parse(files.GetFileInfo("manifest.json"));
        var subsites = await ParseAllSiteDetails(files).ToListAsync();

        return new ParsedSiteDetails(files, details, subsites);
    }

    private static async IAsyncEnumerable<SubsiteInfo> ParseAllSiteDetails(IFileProvider files)
    {
        foreach (var entry in files.GetDirectoryContents(string.Empty))
        {
            if (entry is { Exists: true, IsDirectory: true, Name: { } name })
            {
                if (await ParseSiteDetails(files.GetSubdirectory(name)) is { } details)
                {
                    yield return details;
                }
            }
        }
    }

    private static async ValueTask<SubsiteInfo?> ParseSiteDetails(IFileProvider dir)
    {
        if (dir.GetFileInfo("manifest.json") is { Exists: true, IsDirectory: false } manifest)
        {
            using var stream = manifest.CreateReadStream();

            if (await JsonSerializer.DeserializeAsync<SubsiteInfo?>(stream, JsonSerializerOptions.Web) is { } details)
            {
                details.Content = dir.GetSubdirectory("content");

                details.UsageInstructions = await GetStringOrNul(dir.GetFileInfo("usage.md"));

                return details;
            }
        }

        return null;
    }

    private static async ValueTask<string> GetStringOrNul(IFileInfo? file)
    {
        if (file is { Exists: true, IsDirectory: false })
        {
            using var stream = file.CreateReadStream();
            using var reader = new StreamReader(stream);

            return await reader.ReadToEndAsync();
        }

        return string.Empty;
    }

    private sealed class PortalDetails
    {
        public string Name
        {
            get => field ?? "Portal";
            set;
        }

        public string Description
        {
            get => field ?? string.Empty;
            set;
        }

        public static async ValueTask<PortalDetails> Parse(IFileInfo? file)
        {
            if (file is { Exists: true })
            {
                using var stream = file.CreateReadStream();

                if (await JsonSerializer.DeserializeAsync<PortalDetails>(stream, JsonSerializerOptions.Web) is { } result)
                {
                    return result;
                }
            }

            return new();
        }
    }

    private sealed class ParsedSiteDetails(IFileProvider files, PortalDetails portal, List<SubsiteInfo> subsites) : IDisposable, ISiteDetails
    {
        public string Name => portal.Name;

        public string Description => portal.Description;

        public IEnumerable<SubsiteInfo> Sites => subsites;

        public void Dispose() => (files as IDisposable)?.Dispose();

        public IEnumerable<SubsiteInfo> GetSites(ClaimsPrincipal user)
        {
            foreach (var site in subsites)
            {
                if (site.IsAuthorized(user))
                {
                    yield return site;
                }
            }
        }
    }
}
/// <summary>
/// Represents information about a subsite in the intranet portal
/// </summary>
public class SubsiteInfo
{
    /// <summary>
    /// Unique identifier for the subsite
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the subsite
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Brief description of the subsite
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// URL to navigate to the subsite
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Icon class (e.g., Bootstrap icons, Font Awesome)
    /// </summary>
    public string Icon { get; set; } = "bi bi-globe";

    /// <summary>
    /// Category or department the subsite belongs to
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Detailed usage instructions
    /// </summary>
    public string UsageInstructions { get; set; } = string.Empty;

    [JsonIgnore]
    public IFileProvider? Content { get; set; }

    public IReadOnlyCollection<string> Roles { get; set; } = [];

    /// <summary>
    /// Target audience for the subsite
    /// </summary>
    public string TargetAudience { get; set; } = string.Empty;
}
