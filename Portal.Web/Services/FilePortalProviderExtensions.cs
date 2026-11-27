using Microsoft.Extensions.FileProviders;

namespace Portal.Web.Services;

public static class FilePortalProviderExtensions
{
    public static PortalProviderBuilder AddFile(this PortalProviderBuilder portal)
    {
        portal.AddProvider(_ => portal.Host.Configuration["SITE_PATH"] is { } path
            ? new FilePortalProvider(GetFileProvider(path))
            : null);
        return portal.AddFallbackProvider(_ =>
            new FilePortalProvider(portal.Host.Environment.ContentRootFileProvider.GetSubdirectory("sites")));
    }

    private static IFileProvider GetFileProvider(string path)
    {
        if (File.Exists(path) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new ZipFileProvider(path);
        }

        if (Directory.Exists(path))
        {
            return new PhysicalFileProvider(path);
        }

        throw new FileNotFoundException($"SITE_PATH '{path}' does not exist or is not a valid ZIP file or directory.");
    }
}
