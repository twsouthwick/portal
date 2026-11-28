using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using Portal.Web.Browser;
using Portal.Web.Components;
using Portal.Web.Services;
using Xunit;

namespace Portal.Web.Tests;

public sealed class PortalExperienceTests
{
    // @spec UX-009 UX-011
    [Fact]
    public async Task Grid_RendersFillHeightCardsAndSemanticHeadings()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFluentUIComponents();
        services.AddClientBrowserSession();
        services.AddScoped<ISiteClickTracker, LocalStorageSiteClickTracker>();
        services.AddSingleton<IJSRuntime, NoJavaScriptRuntime>();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<SiteGrid>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(SiteGrid.Category)] = "Tools",
                    [nameof(SiteGrid.Sites)] = new[]
                    {
                        new SiteInfo { Id = "short", Name = "Short", Description = "Short description" },
                        new SiteInfo { Id = "long", Name = "Long", Description = new string('x', 500) }
                    }
                }));
            return result.ToHtmlString();
        });

        var cards = Regex.Matches(html, "<div[^>]*class=\"fluent-card subsite-card\"[^>]*>");
        Assert.Equal(2, cards.Count);
        foreach (Match card in cards)
        {
            Assert.Matches("height:\\s*100%", card.Value);
        }

        Assert.Contains("<h2>Tools</h2>", html);
        Assert.Contains("<h3>Short</h3>", html);
        Assert.Contains("<h3>Long</h3>", html);
        Assert.DoesNotContain("typo=\"Typography.", html);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://portal.test/", "https://portal.test/");

        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class NoJavaScriptRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException("Static rendering must not invoke JavaScript.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException("Static rendering must not invoke JavaScript.");
    }
}
