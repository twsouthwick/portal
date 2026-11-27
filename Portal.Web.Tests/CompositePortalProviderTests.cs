using Portal.Web.Services;
using Xunit;

namespace Portal.Web.Tests;

public sealed class CompositePortalProviderTests
{
    [Fact]
    public async Task GetSiteDetails_CombinesProvidersAndPrefersFirstForDuplicateIds()
    {
        var files = new TestProvider(new PortalDetails("File portal")
        {
            Description = "From files",
            Sites = [new SiteInfo { Id = "home", Name = "File home", Roles = ["admin"] }]
        });
        var kubernetes = new TestProvider(new PortalDetails("Kubernetes")
        {
            Sites =
            [
                new SiteInfo { Id = "HOME", Name = "Kubernetes home" },
                new SiteInfo { Id = "other", Name = "Other" }
            ]
        });
        var provider = new CompositePortalProvider(files, kubernetes);

        var result = await provider.GetSiteDetailsAsync();
        Assert.Equal("File portal", result.Name);
        Assert.Equal("From files", result.Description);
        Assert.Collection(result.Sites,
            site =>
            {
                Assert.Equal("File home", site.Name);
                Assert.Equal(["admin"], site.Roles);
            },
            site => Assert.Equal("Other", site.Name));

        kubernetes.Details = new PortalDetails("Kubernetes")
        {
            Sites = [new SiteInfo { Id = "new", Name = "New" }]
        };
        Assert.Equal(["home", "new"], (await provider.GetSiteDetailsAsync()).Sites.Select(site => site.Id));
    }

    [Fact]
    public async Task GetSiteDetails_PropagatesProviderErrors()
    {
        var provider = new CompositePortalProvider(
            new TestProvider(new PortalDetails("File") { Sites = [new SiteInfo { Id = "home" }] }),
            new FailingProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.GetSiteDetailsAsync());
    }

    private sealed class TestProvider(PortalDetails details) : IPortalProvider
    {
        public PortalDetails Details { get; set; } = details;
        public ValueTask<PortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Details);
    }

    private sealed class FailingProvider : IPortalProvider
    {
        public ValueTask<PortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Kubernetes unavailable");
    }
}
