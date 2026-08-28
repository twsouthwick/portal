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
}
