using k8s;
using System.Text.Json;

namespace Portal.Web.Services;

public sealed class KubernetesPortalProvider : IPortalProvider
{
    private const string DefaultGroup = "portal.swick.app";
    private const string DefaultVersion = "v1alpha1";
    private const string DefaultPlural = "portalsites";

    private readonly IKubernetes _kubernetes;
    private readonly string _namespace;
    private readonly string _group;
    private readonly string _version;
    private readonly string _plural;

    public KubernetesPortalProvider(
        IKubernetes? kubernetes = null,
        string? @namespace = null,
        string? group = null,
        string? version = null,
        string? plural = null)
    {
        _kubernetes = kubernetes ?? new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());
        _namespace = @namespace ?? Environment.GetEnvironmentVariable("PORTAL_KUBERNETES_NAMESPACE") ?? "default";
        _group = group ?? DefaultGroup;
        _version = version ?? DefaultVersion;
        _plural = plural ?? DefaultPlural;
    }

    public async ValueTask<PortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _kubernetes.CustomObjects.ListNamespacedCustomObjectAsync<JsonElement>(
                group: _group,
                version: _version,
                namespaceParameter: _namespace,
                plural: _plural,
                cancellationToken: cancellationToken);

            if (response.ValueKind != JsonValueKind.Object)
            {
                return new PortalDetails("Portal");
            }

            var items = response.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array
                ? itemsElement.EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();

            var sites = items
                .Select(item =>
                {
                    var resource = JsonSerializer.Deserialize<KubernetesPortalSiteResource>(item.GetRawText());
                    return resource?.Spec is null ? null : MapPortalSiteToSiteInfo(resource.Spec);
                })
                .Where(site => site is not null)
                .Select(site => site!)
                .ToArray();

            return new PortalDetails("Portal")
            {
                Sites = sites
            };
        }
        catch (Exception)
        {
            return new PortalDetails("Portal");
        }
    }

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
            "apple" => AppStore.Apple,
            "macos" => AppStore.MacOS,
            "google" => AppStore.Google,
            "microsoft" => AppStore.Microsoft,
            "windows" => AppStore.Windows,
            "linux" => AppStore.Linux,
            _ => AppStore.None
        };
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
}

public sealed class KubernetesPortalAppSpec
{
    public string Name { get; init; } = string.Empty;

    public string Scheme { get; init; } = string.Empty;

    public string? Store { get; init; }

    public string StoreId { get; init; } = string.Empty;
}
