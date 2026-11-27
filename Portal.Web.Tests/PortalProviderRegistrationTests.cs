using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Portal.Web.Services;
using Xunit;

namespace Portal.Web.Tests;

[CollectionDefinition("Kubernetes environment", DisableParallelization = true)]
public sealed class KubernetesEnvironmentCollection;

[Collection("Kubernetes environment")]
public sealed class PortalProviderRegistrationTests
{
    [Fact]
    public async Task FileFallback_IsUsedOnlyWhenNoOtherProviderIsAvailable()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"portal-provider-test-{Guid.NewGuid():N}");
        var sites = Path.Combine(directory, "sites");
        Directory.CreateDirectory(sites);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(sites, "manifest.json"), """{"name":"Bundled"}""");

            var builder = Host.CreateApplicationBuilder();
            builder.Environment.ContentRootFileProvider = new PhysicalFileProvider(directory);
            builder.Configuration["SITE_PATH"] = null;
            builder.AddPortalProvider(portal => portal.AddFile().AddProvider(_ => null));
            using (var host = builder.Build())
            {
                var details = await host.Services.GetRequiredService<IPortalProvider>().GetSiteDetailsAsync();
                Assert.Equal("Bundled", details.Name);
            }

            var otherBuilder = Host.CreateApplicationBuilder();
            otherBuilder.Environment.ContentRootFileProvider = new PhysicalFileProvider(directory);
            otherBuilder.Configuration["SITE_PATH"] = null;
            otherBuilder.AddPortalProvider(portal => portal.AddFile().AddProvider(_ => new TestProvider()));
            using var otherHost = otherBuilder.Build();
            var otherDetails = await otherHost.Services.GetRequiredService<IPortalProvider>().GetSiteDetailsAsync();
            Assert.Equal("Other", otherDetails.Name);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Registration_CombinesConfiguredFilesAndKubeconfig()
    {
        var oldConfig = Environment.GetEnvironmentVariable("KUBECONFIG");
        var oldHost = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
        var directory = Path.Combine(Path.GetTempPath(), $"portal-provider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var serverBuilder = WebApplication.CreateSlimBuilder();
            serverBuilder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var server = serverBuilder.Build();
            server.MapGet("/apis/portal.swick.app/v1alpha1/namespaces/test/portalsites", () =>
                Results.Json(new
                {
                    items = new[]
                    {
                        new { spec = new { id = "shared", name = "Kubernetes shared" } },
                        new { spec = new { id = "cluster", name = "Cluster" } }
                    }
                }));
            await server.StartAsync();
            var address = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

            var sites = Path.Combine(directory, "sites");
            Directory.CreateDirectory(Path.Combine(sites, "shared"));
            await File.WriteAllTextAsync(Path.Combine(sites, "manifest.json"), """{"name":"File portal"}""");
            await File.WriteAllTextAsync(Path.Combine(sites, "shared", "manifest.json"), """{"id":"shared","name":"File shared"}""");
            var kubeconfig = Path.Combine(directory, "kubeconfig");
            await File.WriteAllTextAsync(kubeconfig, $"""
                apiVersion: v1
                kind: Config
                clusters:
                - name: test
                  cluster:
                    server: {address}
                contexts:
                - name: test
                  context:
                    cluster: test
                    user: test
                current-context: test
                users:
                - name: test
                  user:
                    token: test
                """);
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", null);
            Environment.SetEnvironmentVariable("KUBECONFIG", kubeconfig);
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration["SITE_PATH"] = sites;
            builder.Configuration["PORTAL_KUBERNETES_NAMESPACE"] = "test";
            builder.AddPortalProvider(portal => portal.AddFile().AddKubernetes());
            using var host = builder.Build();

            var details = await host.Services.GetRequiredService<IPortalProvider>().GetSiteDetailsAsync();
            Assert.Equal("File portal", details.Name);
            Assert.Collection(details.Sites,
                site => Assert.Equal("File shared", site.Name),
                site => Assert.Equal("Cluster", site.Name));
        }
        finally
        {
            Environment.SetEnvironmentVariable("KUBECONFIG", oldConfig);
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", oldHost);
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TestProvider : IPortalProvider
    {
        public ValueTask<PortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PortalDetails("Other"));
    }
}
