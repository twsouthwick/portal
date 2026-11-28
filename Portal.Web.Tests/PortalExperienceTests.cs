using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.RenderTree;
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
    // @spec UX-013
    [Fact]
    public async Task Categories_SelectBackAndFilterPreserveNavigation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFluentUIComponents();
        services.AddClientBrowserSession();
        services.AddScoped<ISiteClickTracker, LocalStorageSiteClickTracker>();
        services.AddSingleton<IJSRuntime, NoJavaScriptRuntime>();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new CategoryRenderer(provider);
        var photos = new SiteInfo { Id = "photos", Name = "Photos app", Categories = ["Media", "Family"] };
        var films = new SiteInfo { Id = "films", Name = "Films app", Categories = ["Media"] };
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.Show([photos, films]);
            await renderer.Click("Family");
            Assert.Contains("Photos app", renderer.Text());
            Assert.DoesNotContain("Films app", renderer.Text());
            await renderer.Click(null);
            Assert.DoesNotContain("Photos app", renderer.Text());
            await renderer.Click("Media");
            Assert.Contains("Films app", renderer.Text());
            await renderer.Show([films]);
            Assert.Contains("Films app", renderer.Text());
            Assert.DoesNotContain("Photos app", renderer.Text());
            await renderer.Show([photos with { Categories = ["Family"] }]);
            Assert.DoesNotContain("Photos app", renderer.Text());
            Assert.Contains("Family", renderer.Text());
        });
    }

    // @spec UX-013
    [Fact]
    public async Task Categories_InitiallyRenderOnlyCategoriesWithCounts()
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
            var result = await renderer.RenderComponentAsync<SiteCategoryView>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(SiteCategoryView.Sites)] = new[]
                    {
                        new SiteInfo { Id = "photos", Name = "Photos app", Categories = ["Media", "Family"] },
                        new SiteInfo { Id = "films", Name = "Films app", Categories = ["Media"] },
                        new SiteInfo { Id = "wiki", Name = "Wiki app" }
                    }
                }));
            return result.ToHtmlString();
        });
        Assert.DoesNotContain("Photos app", html);
        Assert.DoesNotContain("Films app", html);
        Assert.DoesNotContain("subsite-card", html);
        Assert.Contains("data-category=\"Family\"", html);
        Assert.Contains("data-category=\"Media\"", html);
        Assert.Contains("data-category=\"Other\"", html);
        Assert.Contains("2 sites", html);
        Assert.True(html.IndexOf("data-category=\"Family\"", StringComparison.Ordinal)
            < html.IndexOf("data-category=\"Media\"", StringComparison.Ordinal));
    }

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

    // @spec UX-012
    [Theory]
    [InlineData("1.4.1-alpha.0.3+0123456789abcdef", "1.4.1-alpha.0.3")]
    [InlineData("2.0.0", "2.0.0")]
    public void PortalVersion_RemovesBuildMetadata(string informationalVersion, string expected)
    {
        Assert.Equal(expected, new PortalVersion(informationalVersion).Value);
    }

    // @spec UX-012
    [Fact]
    public async Task SignedOutLayout_RendersVersionFooter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new PortalVersion("1.4.1-alpha.0.3+abc"));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<Portal.Web.Components.Layout.SignedOutLayout>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(LayoutComponentBase.Body)] = (RenderFragment)(builder => builder.AddContent(0, "Signed out"))
                }));
            return result.ToHtmlString();
        });

        Assert.Matches("<footer[^>]*class=\"version-footer\"[^>]*>Version 1\\.4\\.1-alpha\\.0\\.3</footer>", html);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://portal.test/", "https://portal.test/");

        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class NoJavaScriptRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "Blazor._internal.domWrapper.focus")
                return ValueTask.FromResult(default(TValue)!);
            throw new NotSupportedException("Rendering must not invoke other JavaScript.");
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException("Static rendering must not invoke JavaScript.");
    }

#pragma warning disable BL0006 // Exercise actual component events without introducing another test framework.
    private sealed class CategoryRenderer : Renderer
    {
        public CategoryRenderer(IServiceProvider services)
            : base(services, services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>())
        {
            ElementReferenceContext = new WebElementReferenceContext(services.GetRequiredService<IJSRuntime>());
        }

        private int? rootId;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        protected override void HandleException(Exception exception) => throw new InvalidOperationException("Component rendering failed.", exception);

        public Task Show(SiteInfo[] sites)
        {
            rootId ??= AssignRootComponentId(InstantiateComponent(typeof(SiteCategoryView)));
            return RenderRootComponentAsync(rootId.Value, ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(SiteCategoryView.Sites)] = sites }));
        }

        private IEnumerable<RenderTreeFrame> Frames(int componentId)
        {
            var frames = GetCurrentRenderTreeFrames(componentId);
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames.Array[i];
                yield return frame;
                if (frame.FrameType == RenderTreeFrameType.Component)
                    foreach (var child in Frames(frame.ComponentId))
                        yield return child;
            }
        }

        public string Text() => string.Join(" ", Frames(rootId!.Value)
            .Where(frame => frame.FrameType is RenderTreeFrameType.Text or RenderTreeFrameType.Markup)
            .Select(frame => frame.FrameType == RenderTreeFrameType.Text ? frame.TextContent : frame.MarkupContent));

        public Task Click(string? category)
        {
            var frames = Frames(rootId!.Value).ToArray();
            for (var i = 0; i < frames.Length; i++)
            {
                if (frames[i].FrameType != RenderTreeFrameType.Element || frames[i].ElementName != "button")
                    continue;
                var attributes = frames.Skip(i + 1).TakeWhile(frame => frame.FrameType == RenderTreeFrameType.Attribute).ToArray();
                var value = attributes.FirstOrDefault(frame => frame.AttributeName == "data-category").AttributeValue as string;
                if (value != category)
                    continue;
                var handler = attributes.Single(frame => frame.AttributeName == "onclick").AttributeEventHandlerId;
                return DispatchEventAsync(handler, null, new MouseEventArgs());
            }
            throw new InvalidOperationException($"Category button not found: {category ?? "Back"}");
        }
    }
#pragma warning restore BL0006
}
