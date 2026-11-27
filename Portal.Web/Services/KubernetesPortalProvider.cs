using k8s;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Portal.Web.Services;

public sealed partial class KubernetesPortalProvider : IPortalProvider
{
    private const string Group = "portal.swick.app";
    private const string Version = "v1alpha1";
    private const string Plural = "portalsites";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IKubernetes _kubernetes;
    private readonly string _namespace;
    private readonly string _group;
    private readonly string _version;
    private readonly string _plural;

    public KubernetesPortalProvider(
        IKubernetes? kubernetes = null, string? @namespace = null,
        string? group = null, string? version = null, string? plural = null)
    {
        _kubernetes = kubernetes ?? new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());
        _namespace = @namespace ?? Environment.GetEnvironmentVariable("PORTAL_KUBERNETES_NAMESPACE") ?? "default";
        _group = group ?? Group;
        _version = version ?? Version;
        _plural = plural ?? Plural;
        if (string.IsNullOrWhiteSpace(_namespace))
        {
            throw new ArgumentException("Kubernetes namespace must not be empty.", nameof(@namespace));
        }
    }

    public async ValueTask<PortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _kubernetes.CustomObjects.ListNamespacedCustomObjectAsync<JsonElement>(
            group: _group,
            version: _version,
            namespaceParameter: _namespace,
            plural: _plural,
            cancellationToken: cancellationToken);

        if (response.ValueKind != JsonValueKind.Object ||
            !response.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Kubernetes returned an invalid PortalSite list.");
        }

        var sites = new List<SiteInfo>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.EnumerateArray())
        {
            var resource = item.Deserialize<KubernetesPortalSiteResource>(JsonOptions);
            var spec = resource?.Spec ?? throw new InvalidDataException("PortalSite is missing its spec.");
            if (string.IsNullOrEmpty(spec.Id) || spec.Id.Length > 63 ||
                !SiteIdPattern().IsMatch(spec.Id) || string.IsNullOrWhiteSpace(spec.Name))
            {
                throw new InvalidDataException("PortalSite must have a valid id and a non-empty name.");
            }

            if (!ids.Add(spec.Id))
            {
                throw new InvalidDataException($"Duplicate PortalSite id '{spec.Id}'.");
            }

            var site = MapPortalSiteToSiteInfo(spec);
            if (spec.ContentRef is { } contentRef)
            {
                if (string.IsNullOrWhiteSpace(contentRef.Name))
                {
                    throw new InvalidDataException($"PortalSite '{spec.Id}' has an invalid contentRef.");
                }

                var configMap = await _kubernetes.CoreV1.ReadNamespacedConfigMapAsync(
                    contentRef.Name, _namespace, cancellationToken: cancellationToken);
                var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var entry in configMap.Data ?? new Dictionary<string, string>())
                {
                    files.Add(entry.Key, Encoding.UTF8.GetBytes(entry.Value));
                }
                foreach (var entry in configMap.BinaryData ?? new Dictionary<string, byte[]>())
                {
                    files.Add(entry.Key, entry.Value);
                }

                if (contentRef.Key is { } key)
                {
                    if (!files.TryGetValue(key, out var value))
                    {
                        throw new InvalidDataException($"ConfigMap '{contentRef.Name}' does not contain key '{key}'.");
                    }
                    files = new Dictionary<string, byte[]>(StringComparer.Ordinal) { [key] = value };
                }

                site = site with { Content = new ConfigMapFileProvider(files) };
            }
            sites.Add(site);
        }

        return new PortalDetails("Portal") { Sites = sites };
    }

    [GeneratedRegex("^[a-z0-9]([-a-z0-9]*[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex SiteIdPattern();

    public static SiteInfo MapPortalSiteToSiteInfo(KubernetesPortalSiteSpec spec)
    {
        return new SiteInfo
        {
            Id = spec.Id,
            Name = spec.Name,
            Description = spec.Description,
            Url = spec.Url,
            Apps = spec.Apps.Select(app => new SiteApp
            {
                Name = app.Name,
                Scheme = app.Scheme,
                Store = MapStore(app.Store),
                StoreId = app.StoreId
            }).ToArray(),
            Icon = spec.Icon,
            Categories = spec.Categories ?? [],
            UsageInstructions = spec.UsageInstructions ?? string.Empty,
            Roles = spec.Roles ?? [],
            IsPrivate = spec.IsPrivate,
            IsInternal = spec.IsInternal
        };
    }

    private static AppStore MapStore(string? store)
    {
        return store?.ToLowerInvariant() switch
        {
            null or "" or "none" => AppStore.None,
            "apple" => AppStore.Apple,
            "macos" => AppStore.MacOS,
            "google" => AppStore.Google,
            "microsoft" => AppStore.Microsoft,
            "windows" => AppStore.Windows,
            "linux" => AppStore.Linux,
            _ => throw new InvalidDataException($"Unknown app store '{store}'.")
        };
    }

    private sealed class ConfigMapFileProvider(IReadOnlyDictionary<string, byte[]> files) : IFileProvider
    {
        public IFileInfo GetFileInfo(string subpath) =>
            files.TryGetValue(subpath, out var bytes) ? new ConfigMapFileInfo(subpath, bytes) : new NotFoundFileInfo(subpath);

        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    private sealed class ConfigMapFileInfo(string name, byte[] bytes) : IFileInfo
    {
        public bool Exists => true;
        public long Length => bytes.Length;
        public string? PhysicalPath => null;
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.MinValue;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => new MemoryStream(bytes, writable: false);
    }
}

public sealed class KubernetesPortalSiteResource
{
    public KubernetesPortalSiteSpec? Spec { get; init; }
}

public sealed class KubernetesPortalSiteSpec
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Icon { get; init; } = "bi bi-globe";
    public IReadOnlyList<string>? Categories { get; init; }
    public string? UsageInstructions { get; init; }
    public IReadOnlyList<string>? Roles { get; init; }
    public bool IsPrivate { get; init; }
    public bool IsInternal { get; init; }
    public IReadOnlyList<KubernetesPortalAppSpec> Apps { get; init; } = [];
    public KubernetesPortalContentRef? ContentRef { get; init; }
}

public sealed class KubernetesPortalAppSpec
{
    public string Name { get; init; } = string.Empty;
    public string Scheme { get; init; } = string.Empty;
    public string? Store { get; init; }
    public string StoreId { get; init; } = string.Empty;
}

public sealed class KubernetesPortalContentRef
{
    public string Name { get; init; } = string.Empty;
    public string? Key { get; init; }
}
