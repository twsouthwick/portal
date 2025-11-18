using Microsoft.Extensions.FileProviders;
using System.Security.Claims;
using System.Text.Json;

namespace Portal.Web.Services;

internal sealed class FilePortalProvider(IFileProvider files) : IPortalProvider
{
    private readonly Task<IPortalDetails> _task = CreateSite(files);

    public async ValueTask<IPortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default) => await _task;

    public static async Task<IPortalDetails> CreateSite(IFileProvider files)
    {
        var details = await PortalDetails.Parse(files.GetFileInfo("manifest.json"));
        var subsites = await ParseAllSiteDetails(files).ToListAsync();

        return new ParsedSiteDetails(files, details, subsites);
    }

    private static async IAsyncEnumerable<SiteInfo> ParseAllSiteDetails(IFileProvider files)
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

    private static async ValueTask<SiteInfo?> ParseSiteDetails(IFileProvider dir)
    {
        if (dir.GetFileInfo("manifest.json") is { Exists: true, IsDirectory: false } manifest)
        {
            using var stream = manifest.CreateReadStream();

            if (await JsonSerializer.DeserializeAsync<SiteInfo?>(stream, JsonSerializerOptions.Web) is { } details)
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

    private sealed class ParsedSiteDetails(IFileProvider files, PortalDetails portal, List<SiteInfo> subsites) : IDisposable, IPortalDetails
    {
        public string Name => portal.Name;

        public string Description => portal.Description;

        public IEnumerable<SiteInfo> Sites => subsites;

        public void Dispose() => (files as IDisposable)?.Dispose();

        public IEnumerable<SiteInfo> GetSites(ClaimsPrincipal user)
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
