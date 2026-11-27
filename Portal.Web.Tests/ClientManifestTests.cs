using System.ComponentModel;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Schema;
using Microsoft.Extensions.FileProviders;
using Portal.Web.Services;
using Xunit;

namespace Portal.Web.Tests;

public sealed class ClientManifestTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // @spec SITE-010, SITE-011, SITE-012
    [Fact]
    public void Create_ProjectsPortalAndClientFacingSiteFields()
    {
        var details = new PortalDetails("Portal")
        {
            Description = "Portal description",
            Sites =
            [
                new SiteInfo
                {
                    Id = "media",
                    Name = "Media",
                    Description = "Media server",
                    Url = "https://media.example",
                    Icon = "bi bi-film",
                    Categories = ["entertainment"],
                    UsageInstructions = "# Use it",
                    IsInternal = true,
                    IsPrivate = true,
                    Apps =
                    [
                        new SiteApp { Name = "Player", Scheme = "player://open", Store = AppStore.Google, StoreId = "com.example.player" },
                    ],
                },
                new SiteInfo { Id = "second", Name = "Second" },
            ],
        };

        var manifest = ClientManifest.Create(details, User());

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("Portal", manifest.Name);
        Assert.Equal("Portal description", manifest.Description);
        Assert.Equal(["media", "second"], manifest.Sites.Select(site => site.Id));

        var site = manifest.Sites.First();
        Assert.Equal("Media", site.Name);
        Assert.Equal("Media server", site.Description);
        Assert.Equal("https://media.example", site.Url);
        Assert.Equal("bi bi-film", site.Icon);
        Assert.Equal(["entertainment"], site.Categories);
        Assert.Equal("# Use it", site.UsageInstructions);
        Assert.True(site.IsInternal);
        Assert.True(site.IsPrivate);
        var app = Assert.Single(site.Apps);
        Assert.Equal("Player", app.Name);
        Assert.Equal("player://open", app.Scheme);
        Assert.Equal(ClientAppStore.Google, app.Store);
        Assert.Equal("com.example.player", app.StoreId);
    }

    // @spec SITE-012, SITE-013
    [Fact]
    public void Serialize_ExcludesServerOnlyFields()
    {
        var details = new PortalDetails("Portal")
        {
            Sites =
            [
                new SiteInfo
                {
                    Id = "site",
                    Url = "https://site.example",
                    Roles = ["admin"],
                    Content = new NullFileProvider(),
                    Apps = [new SiteApp { Name = "App", Scheme = "app://", Store = AppStore.Windows, StoreId = "id" }],
                },
            ],
        };

        var json = JsonSerializer.Serialize(ClientManifest.Create(details, User("admin")), WebJson);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            ["schemaVersion", "name", "description", "sites"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        var site = document.RootElement.GetProperty("sites")[0];
        Assert.Equal(
            ["id", "name", "description", "url", "icon", "categories", "usageInstructions", "isInternal", "isPrivate", "apps"],
            site.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["name", "scheme", "store", "storeId"],
            site.GetProperty("apps")[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal("windows", site.GetProperty("apps")[0].GetProperty("store").GetString());
    }

    // @spec SITE-014
    [Fact]
    public void Create_IncludesOnlyAuthorizedSites()
    {
        var details = new PortalDetails("Portal")
        {
            Sites =
            [
                new SiteInfo { Id = "public" },
                new SiteInfo { Id = "admin", Roles = ["admin"] },
                new SiteInfo { Id = "media", Roles = ["media", "admin"] },
            ],
        };

        Assert.Equal(["public", "media"], ClientManifest.Create(details, User("media")).Sites.Select(site => site.Id));
        Assert.Equal(["public", "admin", "media"], ClientManifest.Create(details, User("admin")).Sites.Select(site => site.Id));
    }

    // @spec SITE-014
    [Fact]
    public void Create_ReturnsEmptySitesWhenNoneAuthorized()
    {
        var details = new PortalDetails("Portal") { Sites = [new SiteInfo { Id = "admin", Roles = ["admin"] }] };

        var manifest = ClientManifest.Create(details, User());

        Assert.Empty(manifest.Sites);
    }

    // @spec SITE-015
    [Theory]
    [InlineData("https://site.example", "https://site.example")]
    [InlineData("http://site.local:8080/path", "http://site.local:8080/path")]
    [InlineData("", null)]
    [InlineData("/relative", null)]
    [InlineData("site.example", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("ftp://site.example", null)]
    [InlineData("file:///etc/passwd", null)]
    public void Create_KeepsOnlyAbsoluteHttpSiteUrls(string url, string? expected)
    {
        var details = new PortalDetails("Portal") { Sites = [new SiteInfo { Id = "site", Url = url }] };

        var site = Assert.Single(ClientManifest.Create(details, User()).Sites);

        Assert.Equal(expected, site.Url);
    }

    // @spec SITE-016
    [Theory]
    [InlineData("audiobookshelf://", true)]
    [InlineData("https://app.example/open", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("JavaScript:alert(1)", false)]
    [InlineData("data:text/html,hi", false)]
    [InlineData("file:///bin/sh", false)]
    [InlineData("vbscript:msgbox", false)]
    [InlineData("relative/path", false)]
    [InlineData("", false)]
    public void Create_OmitsAppsWithUnsafeOrRelativeSchemes(string scheme, bool kept)
    {
        var details = new PortalDetails("Portal")
        {
            Sites =
            [
                new SiteInfo
                {
                    Id = "site",
                    Apps = [new SiteApp { Name = "App", Scheme = scheme, StoreId = "id" }],
                },
            ],
        };

        var site = Assert.Single(ClientManifest.Create(details, User()).Sites);

        Assert.Equal(kept, site.Apps.Count == 1);
    }

    // @spec SITE-018
    [Fact]
    public void Schema_DescribesStoreAsLowercaseStringEnumeration()
    {
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(JsonSerializerOptions.Web, typeof(ClientManifest));

        var store = schema["properties"]!["sites"]!["items"]!["properties"]!["apps"]!["items"]!["properties"]!["store"]!;

        Assert.Equal(
            ["none", "apple", "macos", "google", "microsoft", "windows", "linux"],
            store["enum"]!.AsArray().Select(value => value!.GetValue<string>()));
    }

    // @spec SITE-018
    [Theory]
    [InlineData(typeof(ClientManifest))]
    [InlineData(typeof(ClientSite))]
    [InlineData(typeof(ClientSiteApp))]
    public void Schema_DescribesEveryTypeAndField(Type type)
    {
        Assert.False(string.IsNullOrWhiteSpace(type.GetCustomAttribute<DescriptionAttribute>()?.Description));
        Assert.All(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => Assert.False(
                string.IsNullOrWhiteSpace(property.GetCustomAttribute<DescriptionAttribute>()?.Description),
                $"{type.Name}.{property.Name} has no description."));
    }

    private static ClaimsPrincipal User(params string[] roles)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "user"), .. roles.Select(role => new Claim(ClaimTypes.Role, role))],
            "Test");
        return new ClaimsPrincipal(identity);
    }
}
