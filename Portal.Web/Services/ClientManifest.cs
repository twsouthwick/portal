using System.Collections.Frozen;
using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Portal.Web.Services;

// @spec SITE-018
[Description("Versioned, client-safe portal manifest containing the sites the caller is authorized to see.")]
public sealed record ClientManifest
{
    public const int CurrentSchemaVersion = 1;

    private static readonly FrozenSet<string> BlockedAppSchemes =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "javascript", "data", "file", "vbscript");

    [Description("Manifest schema version. Additive optional fields keep the version; breaking changes increment it.")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [Description("Portal display name.")]
    public required string Name { get; init; }

    [Description("Portal description.")]
    public required string Description { get; init; }

    [Description("Sites the caller is authorized to see, in provider order.")]
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
        Store = ToClientStore(app.Store),
        StoreId = app.StoreId,
    };

    private static ClientAppStore ToClientStore(AppStore store) => store switch
    {
        AppStore.Apple => ClientAppStore.Apple,
        AppStore.MacOS => ClientAppStore.MacOS,
        AppStore.Google => ClientAppStore.Google,
        AppStore.Microsoft => ClientAppStore.Microsoft,
        AppStore.Windows => ClientAppStore.Windows,
        AppStore.Linux => ClientAppStore.Linux,
        _ => ClientAppStore.None,
    };

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    // Absolute-path strings parse as file: URIs on Unix, so the blocked-scheme check also rejects them.
    private static bool IsAllowedAppScheme(string? scheme) =>
        Uri.TryCreate(scheme, UriKind.Absolute, out var uri) && !BlockedAppSchemes.Contains(uri.Scheme);
}

// @spec SITE-018
[Description("Client-facing site metadata. Server-only fields such as content and roles are intentionally absent.")]
public sealed record ClientSite
{
    [Description("Stable site identifier.")]
    public required string Id { get; init; }

    [Description("Site display name.")]
    public required string Name { get; init; }

    [Description("Site description.")]
    public required string Description { get; init; }

    [Description("Absolute http or https site URL; null when the configured URL is missing or invalid.")]
    public string? Url { get; init; }

    [Description("Site icon CSS class, e.g. a Bootstrap Icons class.")]
    public required string Icon { get; init; }

    [Description("Site categories.")]
    public required IReadOnlyList<string> Categories { get; init; }

    [Description("Raw Markdown usage instructions.")]
    public required string UsageInstructions { get; init; }

    [Description("Whether the site is reachable only from the internal network; the client decides how to present it.")]
    public bool IsInternal { get; init; }

    [Description("Whether the site is private; the client decides how to present it.")]
    public bool IsPrivate { get; init; }

    [Description("Launchable apps for the site, for every platform; the client selects apps for its own OS.")]
    public required IReadOnlyList<ClientSiteApp> Apps { get; init; }
}

// @spec SITE-018
[Description("Client-facing app launch metadata.")]
public sealed record ClientSiteApp
{
    [Description("App display name.")]
    public required string Name { get; init; }

    [Description("Absolute launch URI for the app.")]
    public required string Scheme { get; init; }

    [Description("Store or platform that distributes the app.")]
    public required ClientAppStore Store { get; init; }

    [Description("Store URL or store-specific app ID.")]
    public required string StoreId { get; init; }
}

// @spec SITE-018
[Description("Store or platform that distributes an app.")]
[JsonConverter(typeof(JsonStringEnumConverter<ClientAppStore>))]
public enum ClientAppStore
{
    [JsonStringEnumMemberName("none")]
    None,

    [JsonStringEnumMemberName("apple")]
    Apple,

    [JsonStringEnumMemberName("macos")]
    MacOS,

    [JsonStringEnumMemberName("google")]
    Google,

    [JsonStringEnumMemberName("microsoft")]
    Microsoft,

    [JsonStringEnumMemberName("windows")]
    Windows,

    [JsonStringEnumMemberName("linux")]
    Linux,
}
