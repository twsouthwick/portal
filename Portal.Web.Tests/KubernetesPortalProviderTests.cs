using System.Net;
using System.Text;
using k8s;
using Microsoft.Extensions.Logging;
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

    // @spec SITE-005 SITE-007
    [Fact]
    public async Task GetSiteDetails_ReadsCamelCaseResourcesAndConfigMapContent()
    {
        var handler = new KubernetesHandler
        {
            Sites = """
                {"items":[{"metadata":{"name":"home","namespace":"test-ns"},"spec":{"id":"home","name":"Home","roles":["ops"],
                "isPrivate":true,"usageInstructions":"Hello","apps":[{"name":"App","store":"google","storeId":"app"}],
                "contentRef":{"name":"home-content","key":"logo.png"}}}]}
                """,
            ConfigMap = """{"metadata":{"name":"home-content"},"data":{"other.txt":"hidden"},"binaryData":{"logo.png":"AQID"}}"""
        };
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client);

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
        Assert.Contains("/apis/portal.swick.app/v1alpha1/portalsites", handler.Paths);
        Assert.Contains("/api/v1/namespaces/test-ns/configmaps/home-content", handler.Paths);
    }

    // @spec SITE-005 SITE-007
    [Fact]
    public async Task GetSiteAsync_OnlyFetchesContentForRequestedSite()
    {
        var handler = new KubernetesHandler
        {
            Sites = """
                {"items":[
                  {"metadata":{"namespace":"first-ns"},"spec":{"id":"first","name":"First","contentRef":{"name":"first-content"}}},
                  {"metadata":{"namespace":"requested-ns"},"spec":{"id":"requested","name":"Requested","contentRef":{"name":"requested-content"}}}
                ]}
                """,
            ConfigMap = """{"data":{"file.txt":"content"}}"""
        };
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client);

        var site = await provider.GetSiteAsync("requested");

        Assert.Equal("requested", site?.Id);
        Assert.Equal(
            ["/apis/portal.swick.app/v1alpha1/portalsites",
             "/api/v1/namespaces/requested-ns/configmaps/requested-content"],
            handler.Paths);
    }

    // @spec SITE-005 SITE-007
    [Fact]
    public async Task GetSiteDetails_ReadsContentInEachResourceNamespace()
    {
        var handler = new KubernetesHandler
        {
            Sites = """
                {"items":[
                  {"metadata":{"namespace":"first-ns"},"spec":{"id":"first","name":"First","contentRef":{"name":"content"}}},
                  {"metadata":{"namespace":"second-ns"},"spec":{"id":"second","name":"Second","contentRef":{"name":"content"}}}
                ]}
                """,
            ConfigMap = """{"data":{"file.txt":"content"}}"""
        };
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client);

        var sites = (await provider.GetSiteDetailsAsync()).Sites.ToArray();

        Assert.Equal(["first", "second"], sites.Select(site => site.Id));
        Assert.All(sites, site => Assert.True(site.Content?.GetFileInfo("file.txt").Exists));
        Assert.Equal(
            ["/apis/portal.swick.app/v1alpha1/portalsites",
             "/api/v1/namespaces/first-ns/configmaps/content",
             "/api/v1/namespaces/second-ns/configmaps/content"],
            handler.Paths);
    }

    // @spec SITE-007
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, " ")]
    [InlineData(true, " ")]
    public async Task ContentReference_RejectsMissingNamespace(bool lookup, string? resourceNamespace)
    {
        var handler = new KubernetesHandler
        {
            Sites = System.Text.Json.JsonSerializer.Serialize(new
            {
                items = new[]
                {
                    new
                    {
                        metadata = new { @namespace = resourceNamespace },
                        spec = new { id = "home", name = "Home", contentRef = new { name = "content" } }
                    }
                }
            })
        };
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client);

        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            if (lookup)
                await provider.GetSiteAsync("home");
            else
                await provider.GetSiteDetailsAsync();
        });

        Assert.Contains("namespace", error.Message);
        Assert.Single(handler.Paths);
    }

    // @spec SITE-020
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossNamespaceDuplicates_AreRejected(bool lookup)
    {
        var handler = new KubernetesHandler
        {
            Sites = """
                {"items":[
                  {"metadata":{"namespace":"first-ns"},"spec":{"id":"home","name":"First"}},
                  {"metadata":{"namespace":"second-ns"},"spec":{"id":"home","name":"Second"}}
                ]}
                """
        };
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client);

        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            if (lookup)
                await provider.GetSiteAsync("HOME");
            else
                await provider.GetSiteDetailsAsync();
        });
        Assert.Contains("Duplicate PortalSite id", error.Message);
    }

    [Fact]
    public async Task GetSiteDetails_LogsAndIgnoresUnknownAppStore()
    {
        var handler = new KubernetesHandler
        {
            Sites = """
                {"items":[
                  {"spec":{"id":"legacy","name":"Legacy","apps":[{"name":"App","store":"android","storeId":"app"}]}},
                  {"spec":{"id":"valid","name":"Valid"}}
                ]}
                """
        };
        var logger = new TestLogger<KubernetesPortalProvider>();
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client, logger: logger);

        var sites = (await provider.GetSiteDetailsAsync()).Sites.ToArray();

        Assert.Equal(2, sites.Length);
        Assert.Equal(AppStore.None, Assert.Single(sites[0].Apps).Store);
        Assert.Contains(logger.Warnings, warning => warning.Contains("android") && warning.Contains("legacy"));
    }

    [Fact]
    public async Task GetSiteDetails_ReflectsChangesAndPropagatesFailures()
    {
        var handler = new KubernetesHandler { Sites = """{"items":[]}""" };
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client);
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

    // @spec SITE-021 SITE-022
    [Theory]
    [InlineData("Deployment", "deployments", 0, SiteStatus.Unavailable)]
    [InlineData("Deployment", "deployments", 1, SiteStatus.Available)]
    [InlineData("StatefulSet", "statefulsets", 0, SiteStatus.Unavailable)]
    [InlineData("StatefulSet", "statefulsets", 2, SiteStatus.Available)]
    [InlineData("ReplicaSet", "replicasets", 0, SiteStatus.Unavailable)]
    [InlineData("ReplicaSet", "replicasets", 3, SiteStatus.Available)]
    [InlineData("Deployment", "deployments", null, SiteStatus.Available)]
    public async Task SiteStatus_UsesDesiredReplicas(
        string kind, string plural, int? replicas, SiteStatus expected)
    {
        var handler = new KubernetesHandler
        {
            Workload = replicas is null
                ? """{"spec":{},"status":{"readyReplicas":0}}"""
                : $$$"""{"spec":{"replicas":{{{replicas}}}},"status":{"readyReplicas":99}}"""
        };
        using var client = CreateClient(handler);
        var statusProvider = new KubernetesSiteStatusProvider(client);
        var resource = CreateLinkedResource(kind);

        Assert.Equal(expected, await statusProvider.GetSiteStatusAsync(resource));
        Assert.Equal([$"/apis/apps/v1/namespaces/site-ns/{plural}/workload"], handler.Paths);

        handler.Paths.Clear();
        Assert.Equal(SiteStatus.Available, await statusProvider.GetSiteStatusAsync(
            new KubernetesPortalSiteResource { Spec = new() { Id = "unlinked", Name = "Unlinked" } }));
        Assert.Empty(handler.Paths);
    }

    // @spec SITE-022 SITE-023 SITE-025
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LinkedSite_RefreshesAvailabilityBeforeFetchingContent(bool lookup)
    {
        var handler = new KubernetesHandler
        {
            Sites = """
                {"items":[{"metadata":{"namespace":"site-ns"},"spec":{"id":"home","name":"Home",
                "workloadRef":{"kind":"Deployment","name":"workload"},"contentRef":{"name":"content"}}}]}
                """,
            Workload = """{"spec":{"replicas":0}}""",
            ConfigMap = """{"data":{"file.txt":"content"}}"""
        };
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client);
        async Task<SiteInfo?> LoadAsync() => lookup
            ? await provider.GetSiteAsync("home")
            : (await provider.GetSiteDetailsAsync()).Sites.SingleOrDefault();

        Assert.Null(await LoadAsync());
        Assert.Empty(ClientManifest.Create(await provider.GetSiteDetailsAsync(), new()).Sites);
        Assert.DoesNotContain(handler.Paths, path => path.Contains("/configmaps/"));
        handler.Workload = """{"spec":{"replicas":1},"status":{"readyReplicas":0}}""";
        Assert.NotNull((await LoadAsync())?.Content);
        Assert.Single(ClientManifest.Create(await provider.GetSiteDetailsAsync(), new()).Sites);

        handler.Paths.Clear();
        handler.WorkloadStatus = HttpStatusCode.NotFound;
        Assert.Null(await LoadAsync());
        Assert.DoesNotContain(handler.Paths, path => path.Contains("/configmaps/"));

        handler.Sites = """
            {"items":[
            {"metadata":{"namespace":"site-ns"},"spec":{"id":"home","name":"Home",
            "workloadRef":{"kind":"Deployment","name":"workload"}}},
            {"metadata":{"namespace":"other-ns"},"spec":{"id":"home","name":"Duplicate"}}]}
            """;
        await Assert.ThrowsAsync<InvalidDataException>(async () => await LoadAsync());
    }

    // @spec SITE-023 SITE-024
    [Fact]
    public async Task SiteStatus_OnlyTreatsNotFoundAsUnavailableAndLogsReference()
    {
        var handler = new KubernetesHandler { WorkloadStatus = HttpStatusCode.NotFound };
        var logger = new TestLogger<KubernetesSiteStatusProvider>();
        using var client = CreateClient(handler);
        var statusProvider = new KubernetesSiteStatusProvider(client, logger);
        var resource = CreateLinkedResource("StatefulSet");

        Assert.Equal(SiteStatus.Unavailable, await statusProvider.GetSiteStatusAsync(resource));
        Assert.Contains(logger.Warnings, warning =>
            warning.Contains("home") && warning.Contains("site-ns") &&
            warning.Contains("StatefulSet") && warning.Contains("workload"));

        handler.WorkloadStatus = HttpStatusCode.Forbidden;
        await Assert.ThrowsAnyAsync<Exception>(async () => await statusProvider.GetSiteStatusAsync(resource));
        handler.WorkloadStatus = HttpStatusCode.InternalServerError;
        await Assert.ThrowsAnyAsync<Exception>(async () => await statusProvider.GetSiteStatusAsync(resource));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await statusProvider.GetSiteStatusAsync(resource, cancellation.Token));
    }

    // @spec SITE-024
    [Theory]
    [InlineData("Job", "workload", "site-ns")]
    [InlineData("deployment", "workload", "site-ns")]
    [InlineData("Deployment", "", "site-ns")]
    [InlineData("Deployment", "../workload", "site-ns")]
    [InlineData("Deployment", "Workload", "site-ns")]
    [InlineData("Deployment", "workload\n", "site-ns")]
    [InlineData("Deployment", "workload", null)]
    [InlineData("Deployment", "workload", " ")]
    public async Task SiteStatus_RejectsInvalidReferencesWithoutWorkloadRead(
        string kind, string name, string? resourceNamespace)
    {
        var handler = new KubernetesHandler();
        using var client = CreateClient(handler);
        var statusProvider = new KubernetesSiteStatusProvider(client);
        var resource = new KubernetesPortalSiteResource
        {
            Metadata = new() { NamespaceProperty = resourceNamespace },
            Spec = new() { Id = "home", Name = "Home", WorkloadRef = new() { Kind = kind, Name = name } }
        };

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await statusProvider.GetSiteStatusAsync(resource));
        Assert.Empty(handler.Paths);
    }

    // @spec SITE-024
    [Theory]
    [InlineData(253, true)]
    [InlineData(254, false)]
    public async Task SiteStatus_ValidatesWorkloadNameLength(int length, bool valid)
    {
        var handler = new KubernetesHandler();
        using var client = CreateClient(handler);
        var statusProvider = new KubernetesSiteStatusProvider(client);
        var resource = new KubernetesPortalSiteResource
        {
            Metadata = new() { NamespaceProperty = "site-ns" },
            Spec = new()
            {
                Id = "home", Name = "Home",
                WorkloadRef = new() { Kind = "Deployment", Name = new string('a', length) }
            }
        };
        if (valid)
        {
            Assert.Equal(SiteStatus.Available, await statusProvider.GetSiteStatusAsync(resource));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await statusProvider.GetSiteStatusAsync(resource));
            Assert.Empty(handler.Paths);
        }
    }

    // @spec SITE-024
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("""{"spec":{"replicas":-1}}""")]
    [InlineData("""{"spec":{"replicas":"invalid"}}""")]
    public async Task SiteStatus_RejectsMalformedWorkload(string workload)
    {
        var handler = new KubernetesHandler { Workload = workload };
        using var client = CreateClient(handler);
        var statusProvider = new KubernetesSiteStatusProvider(client);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await statusProvider.GetSiteStatusAsync(CreateLinkedResource("ReplicaSet")));
    }

    // @spec SITE-024
    [Fact]
    public async Task LinkedSite_RejectsNamespaceOverride()
    {
        var handler = new KubernetesHandler
        {
            Sites = """
                {"items":[{"metadata":{"namespace":"site-ns"},"spec":{"id":"home","name":"Home",
                "workloadRef":{"kind":"Deployment","name":"workload","namespace":"other-ns"}}}]}
                """
        };
        using var client = CreateClient(handler);
        var provider = new KubernetesPortalProvider(client);

        await Assert.ThrowsAsync<InvalidDataException>(async () => await provider.GetSiteDetailsAsync());
        Assert.Single(handler.Paths);
    }

    // @spec SITE-026
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Provider_UsesInjectedSiteStatusProvider(bool lookup)
    {
        var handler = new KubernetesHandler
        {
            Sites = """{"items":[{"spec":{"id":"home","name":"Home"}}]}"""
        };
        using var client = CreateClient(handler);
        var statusProvider = new StubSiteStatusProvider();
        var provider = new KubernetesPortalProvider(client, siteStatusProvider: statusProvider);
        async Task<SiteInfo?> LoadAsync() => lookup
            ? await provider.GetSiteAsync("home")
            : (await provider.GetSiteDetailsAsync()).Sites.SingleOrDefault();

        Assert.Null(await LoadAsync());
        Assert.Equal("home", statusProvider.LastResource?.Spec?.Id);
        statusProvider.Status = SiteStatus.Available;
        Assert.NotNull(await LoadAsync());
        statusProvider.Error = new InvalidOperationException("status failed");
        Assert.Same(statusProvider.Error,
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await LoadAsync()));
    }

    private static KubernetesPortalSiteResource CreateLinkedResource(string kind) => new()
    {
        Metadata = new() { NamespaceProperty = "site-ns" },
        Spec = new()
        {
            Id = "home", Name = "Home",
            WorkloadRef = new() { Kind = kind, Name = "workload" }
        }
    };

    private sealed class StubSiteStatusProvider : ISiteStatusProvider<KubernetesPortalSiteResource>
    {
        public SiteStatus Status { get; set; } = SiteStatus.Unavailable;
        public Exception? Error { get; set; }
        public KubernetesPortalSiteResource? LastResource { get; private set; }

        public ValueTask<SiteStatus> GetSiteStatusAsync(
            KubernetesPortalSiteResource site, CancellationToken cancellationToken = default)
        {
            LastResource = site;
            return Error is null ? ValueTask.FromResult(Status) : ValueTask.FromException<SiteStatus>(Error);
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }

    private sealed class KubernetesHandler : DelegatingHandler
    {
        public string Sites { get; set; } = """{"items":[]}""";
        public string ConfigMap { get; set; } = "{}";
        public string Workload { get; set; } = """{"spec":{"replicas":1}}""";
        public HttpStatusCode WorkloadStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            var isWorkload = path.StartsWith("/apis/apps/v1/", StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(isWorkload ? WorkloadStatus : Status)
            {
                RequestMessage = request,
                Content = new StringContent(isWorkload ? Workload :
                    path.Contains("/configmaps/") ? ConfigMap : Sites, Encoding.UTF8, "application/json")
            });
        }
    }
}
