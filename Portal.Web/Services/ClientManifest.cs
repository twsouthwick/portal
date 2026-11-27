using System.Collections.Frozen;
using System.Security.Claims;

namespace Portal.Web.Services;

/// <summary>
/// Versioned, client-safe portal manifest projected from <see cref="PortalDetails"/>.
/// </summary>
public sealed record ClientManifest
{
    public const int CurrentSchemaVersion = 1;

    private static readonly FrozenSet<string> BlockedAppSchemes =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "javascript", "data", "file", "vbscript");

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<ClientSite> Sites { get; init; }

    // @spec SITE-010, SITE-011, SITE-012, SITE-013, SITE-014, SITE-015, SITE-016
    public static ClientManifest Create(PortalDetails details, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(user);

        return new ClientManifest
        {
            Name = details.Name,
            Description = details.Description,
            Sites = [.. details.Sites.Where(site => site.IsAuthorized(user)).Select(ToClientSite)],
        };
    }

    private static ClientSite ToClientSite(SiteInfo site) => new()
    {
        Id = site.Id,
        Name = site.Name,
        Description = site.Description,
        Url = IsHttpUrl(site.Url) ? site.Url : null,
        Icon = site.Icon,
        Categories = [.. site.Categories],
        UsageInstructions = site.UsageInstructions,
        IsInternal = site.IsInternal,
        IsPrivate = site.IsPrivate,
        Apps = [.. site.Apps.Where(app => IsAllowedAppScheme(app.Scheme)).Select(ToClientApp)],
    };

    private static ClientSiteApp ToClientApp(SiteApp app) => new()
    {
        Name = app.Name,
        Scheme = app.Scheme,
        Store = app.Store.ToString().ToLowerInvariant(),
        StoreId = app.StoreId,
    };

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    // Absolute-path strings parse as file: URIs on Unix, so the blocked-scheme check also rejects them.
    private static bool IsAllowedAppScheme(string? scheme) =>
        Uri.TryCreate(scheme, UriKind.Absolute, out var uri) && !BlockedAppSchemes.Contains(uri.Scheme);
}

/// <summary>
/// Client-facing site metadata. Server-only fields such as content and roles are intentionally absent.
/// </summary>
public sealed record ClientSite
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public string? Url { get; init; }

    public required string Icon { get; init; }

    public required IReadOnlyList<string> Categories { get; init; }

    /// <summary>
    /// Raw Markdown usage instructions.
    /// </summary>
    public required string UsageInstructions { get; init; }

    public bool IsInternal { get; init; }

    public bool IsPrivate { get; init; }

    public required IReadOnlyList<ClientSiteApp> Apps { get; init; }
}

/// <summary>
/// Client-facing app launch metadata.
/// </summary>
public sealed record ClientSiteApp
{
    public required string Name { get; init; }

    public required string Scheme { get; init; }

    /// <summary>
    /// Lowercase store name, e.g. <c>google</c>.
    /// </summary>
    public required string Store { get; init; }

    public required string StoreId { get; init; }
}
