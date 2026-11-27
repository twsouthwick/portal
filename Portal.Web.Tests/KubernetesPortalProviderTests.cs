using System.Net;
using System.Text;
using k8s;
using Portal.Web.Services;
using Xunit;

namespace Portal.Web.Tests;

public class KubernetesPortalProviderTests
{
    [Fact]
    public void MapPortalSiteToSiteInfo_MapsSiteMetadataAndApps()
    {
        var siteSpec = new KubernetesPortalSiteSpec
        {
            Id = "home",
            Name = "Home",
            Description = "Home automation",
            Url = "https://example.test/home",
            Icon = "bi bi-house",
            Categories = ["home"],
            Roles = ["ops"],
            UsageInstructions = "Open the app",
            IsPrivate = true,
            IsInternal = true,
            Apps =
            [
                new KubernetesPortalAppSpec
                {
                    Name = "Home Assistant",
                    Scheme = "ha://",
                    Store = "google",
                    StoreId = "com.homeassistant"
                }
            ]
        };

        var site = KubernetesPortalProvider.MapPortalSiteToSiteInfo(siteSpec);

        Assert.Equal("home", site.Id);
        Assert.Equal("Home", site.Name);
        Assert.Equal("https://example.test/home", site.Url);
        Assert.Equal(["home"], site.Categories);
        Assert.Equal(["ops"], site.Roles);
        Assert.Equal("Open the app", site.UsageInstructions);
        Assert.True(site.IsPrivate);
        Assert.True(site.IsInternal);
        var app = Assert.Single(site.Apps);
        Assert.Equal("Home Assistant", app.Name);
        Assert.Equal("ha://", app.Scheme);
        Assert.Equal(AppStore.Google, app.Store);
        Assert.Equal("com.homeassistant", app.StoreId);
    }

    [Fact]
    public async Task GetSiteDetails_ReadsCamelCaseResourcesAndConfigMapContent()
    {
        var handler = new KubernetesHandler
        {
            Sites = """
                {"items":[{"metadata":{"name":"home"},"spec":{"id":"home","name":"Home","roles":["ops"],
                "isPrivate":true,"usageInstructions":"Hello","apps":[{"name":"App","store":"google","storeId":"app"}],
                "contentRef":{"name":"home-content","key":"logo.png"}}}]}
                """,
            ConfigMap = """{"metadata":{"name":"home-content"},"data":{"other.txt":"hidden"},"binaryData":{"logo.png":"AQID"}}"""
        };
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client, "test-ns");

        var site = Assert.Single((await provider.GetSiteDetailsAsync()).Sites);

        Assert.Equal("home", site.Id);
        Assert.Equal("Home", site.Name);
        Assert.Equal(["ops"], site.Roles);
        Assert.True(site.IsPrivate);
        Assert.Equal("Hello", site.UsageInstructions);
        Assert.Equal(AppStore.Google, Assert.Single(site.Apps).Store);
        Assert.NotNull(site.Content);
        using var stream = site.Content.GetFileInfo("logo.png").CreateReadStream();
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes.ToArray());
        Assert.False(site.Content.GetFileInfo("other.txt").Exists);
        Assert.Contains("/apis/portal.swick.app/v1alpha1/namespaces/test-ns/portalsites", handler.Paths);
        Assert.Contains("/api/v1/namespaces/test-ns/configmaps/home-content", handler.Paths);
    }

    [Fact]
    public async Task GetSiteAsync_OnlyFetchesContentForRequestedSite()
    {
        var handler = new KubernetesHandler
        {
            Sites = """
                {"items":[
                  {"spec":{"id":"first","name":"First","contentRef":{"name":"first-content"}}},
                  {"spec":{"id":"requested","name":"Requested","contentRef":{"name":"requested-content"}}}
                ]}
                """,
            ConfigMap = """{"data":{"file.txt":"content"}}"""
        };
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client, "test-ns");

        var site = await provider.GetSiteAsync("requested");

        Assert.Equal("requested", site?.Id);
        Assert.Equal(
            ["/apis/portal.swick.app/v1alpha1/namespaces/test-ns/portalsites",
             "/api/v1/namespaces/test-ns/configmaps/requested-content"],
            handler.Paths);
    }

    [Fact]
    public async Task GetSiteDetails_ReflectsChangesAndPropagatesFailures()
    {
        var handler = new KubernetesHandler { Sites = """{"items":[]}""" };
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client, "test-ns");
        Assert.Empty((await provider.GetSiteDetailsAsync()).Sites);

        handler.Sites = """{"items":[{"spec":{"id":"new","name":"New","roles":["admin"]}}]}""";
        Assert.Equal(["admin"], Assert.Single((await provider.GetSiteDetailsAsync()).Sites).Roles);

        handler.Sites = """{"items":[{"spec":{"id":"new","name":"New"}},{"spec":{"id":"new","name":"Duplicate"}}]}""";
        await Assert.ThrowsAsync<InvalidDataException>(async () => await provider.GetSiteDetailsAsync());
        handler.Status = HttpStatusCode.Forbidden;
        await Assert.ThrowsAnyAsync<Exception>(async () => await provider.GetSiteDetailsAsync());
    }

    private static Kubernetes CreateClient(KubernetesHandler handler) =>
        new(new KubernetesClientConfiguration { Host = "http://localhost" }, handler);

    private sealed class KubernetesHandler : DelegatingHandler
    {
        public string Sites { get; set; } = """{"items":[]}""";
        public string ConfigMap { get; set; } = "{}";
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                RequestMessage = request,
                Content = new StringContent(path.Contains("/configmaps/") ? ConfigMap : Sites, Encoding.UTF8, "application/json")
            });
        }
    }
}
