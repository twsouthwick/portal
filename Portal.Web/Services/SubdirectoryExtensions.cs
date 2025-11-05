using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Portal.Web.Services;

public static class SubdirectoryExtensions
{
    public static IFileProvider GetSubdirectory(this IFileProvider fileProvider, string subdirectory)
        => new SubDirectoryFileProvider(fileProvider, subdirectory);

    private sealed class SubDirectoryFileProvider(IFileProvider other, string subDirectory) : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath) => other.GetDirectoryContents(GetDir(subpath));


        public IFileInfo GetFileInfo(string subpath) => other.GetFileInfo(GetDir(subpath));

        public IChangeToken Watch(string filter) => other.Watch(GetDir(filter));

        private string GetDir(string subpath)
        {
            return subDirectory + "/" + subpath;
        }
    }
}
