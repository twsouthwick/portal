using Microsoft.Extensions.FileProviders;
using System.Text.Json;

namespace Portal.Web.Services;

internal sealed class FilePortalProvider(IFileProvider files) : IPortalProvider
{
    private readonly Task<Services.PortalDetails> _task = CreateSite(files);

    public async ValueTask<Services.PortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default) => await _task;

    public static async Task<Services.PortalDetails> CreateSite(IFileProvider files)
    {
        var details = await PortalDetails.Parse(files.GetFileInfo("manifest.json"));
        var subsites = await ParseAllSiteDetails(files).ToListAsync();

        return new(details.Name)
        {
            Description = details.Description,
            Sites = subsites,
        };
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
                var content = dir.GetSubdirectory("content");
                var usageInstructions = await GetStringOrNull(dir.GetFileInfo("usage.md"));

                return details with
                {
                    Content = content,
                    UsageInstructions = usageInstructions
                };
            }
        }

        return null;
    }

    private static async ValueTask<string> GetStringOrNull(IFileInfo? file)
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
}
