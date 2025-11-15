using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.IO.Compression;

namespace Portal.Web.Services;

/// <summary>
/// File provider that reads files from a ZIP archive
/// </summary>
public sealed class ZipFileProvider : IFileProvider, IDisposable
{
    private readonly ZipArchive _archive;
    private readonly string _basePath;
    private bool _disposed;

    public ZipFileProvider(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException($"ZIP file not found: {zipFilePath}", zipFilePath);
        }

        var fileStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);
        _basePath = string.Empty;
    }

    private ZipFileProvider(ZipArchive archive, string basePath)
    {
        _archive = archive;
        _basePath = basePath;
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var fullPath = GetFullPath(subpath);
        var entries = new List<IFileInfo>();

        // Normalize the path
        var searchPath = fullPath.TrimEnd('/');
        if (!string.IsNullOrEmpty(searchPath))
        {
            searchPath += "/";
        }

        var foundEntries = new HashSet<string>();

        foreach (var entry in _archive.Entries)
        {
            var entryPath = entry.FullName;

            // Check if this entry is in the requested directory
            if (entryPath.StartsWith(searchPath, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = entryPath.Substring(searchPath.Length);

                // Get the first segment (immediate child)
                var slashIndex = relativePath.IndexOf('/');
                var childName = slashIndex >= 0 ? relativePath.Substring(0, slashIndex) : relativePath;

                if (!string.IsNullOrEmpty(childName) && foundEntries.Add(childName))
                {
                    var isDirectory = slashIndex >= 0;
                    entries.Add(new ZipFileInfo(entry, childName, isDirectory));
                }
            }
        }

        return new ZipDirectoryContents(entries);
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var fullPath = GetFullPath(subpath);

        // Try to find exact match
        var entry = _archive.GetEntry(fullPath);
        if (entry != null)
        {
            return new ZipFileInfo(entry, Path.GetFileName(fullPath), false);
        }

        // Check if it's a directory
        var dirPath = fullPath.TrimEnd('/') + "/";
        var hasChildren = _archive.Entries.Any(e => e.FullName.StartsWith(dirPath, StringComparison.OrdinalIgnoreCase));
        
        if (hasChildren)
        {
            return new ZipFileInfo(null, Path.GetFileName(fullPath.TrimEnd('/')), true);
        }

        return new NotFoundFileInfo(subpath);
    }

    public IChangeToken Watch(string filter)
    {
        // ZIP files don't support change notifications
        return NullChangeToken.Singleton;
    }

    private string GetFullPath(string subpath)
    {
        if (string.IsNullOrEmpty(subpath))
        {
            return _basePath;
        }

        var normalizedPath = subpath.Replace('\\', '/').TrimStart('/');
        
        if (string.IsNullOrEmpty(_basePath))
        {
            return normalizedPath;
        }

        return $"{_basePath}/{normalizedPath}";
    }

    public IFileProvider GetSubdirectory(string subdirectory)
    {
        var newBasePath = GetFullPath(subdirectory);
        return new ZipFileProvider(_archive, newBasePath);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Only dispose the archive if we own it (when basePath is empty, meaning this is the root provider)
            if (string.IsNullOrEmpty(_basePath))
            {
                _archive?.Dispose();
            }
            _disposed = true;
        }
    }

    private sealed class ZipFileInfo : IFileInfo
    {
        private readonly ZipArchiveEntry? _entry;
        private readonly string _name;
        private readonly bool _isDirectory;

        public ZipFileInfo(ZipArchiveEntry? entry, string name, bool isDirectory)
        {
            _entry = entry;
            _name = name;
            _isDirectory = isDirectory;
        }

        public bool Exists => true;
        public long Length => _entry?.Length ?? 0;
        public string? PhysicalPath => null;
        public string Name => _name;
        public DateTimeOffset LastModified => _entry?.LastWriteTime ?? DateTimeOffset.MinValue;
        public bool IsDirectory => _isDirectory;

        public Stream CreateReadStream()
        {
            if (_isDirectory || _entry == null)
            {
                throw new InvalidOperationException("Cannot create a stream for a directory.");
            }

            // Copy the entry to a MemoryStream to allow multiple reads and seeking
            var memoryStream = new MemoryStream();
            using (var entryStream = _entry.Open())
            {
                entryStream.CopyTo(memoryStream);
            }
            memoryStream.Position = 0;
            return memoryStream;
        }
    }

    private sealed class ZipDirectoryContents : IDirectoryContents
    {
        private readonly List<IFileInfo> _entries;

        public ZipDirectoryContents(List<IFileInfo> entries)
        {
            _entries = entries;
        }

        public bool Exists => true;

        public IEnumerator<IFileInfo> GetEnumerator() => _entries.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class NotFoundFileInfo : IFileInfo
    {
        private readonly string _name;

        public NotFoundFileInfo(string name)
        {
            _name = name;
        }

        public bool Exists => false;
        public long Length => -1;
        public string? PhysicalPath => null;
        public string Name => _name;
        public DateTimeOffset LastModified => DateTimeOffset.MinValue;
        public bool IsDirectory => false;

        public Stream CreateReadStream()
        {
            throw new FileNotFoundException(_name);
        }
    }
}
