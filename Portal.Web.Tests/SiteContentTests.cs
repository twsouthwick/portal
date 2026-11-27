using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Portal.Web.Services;
using Xunit;

namespace Portal.Web.Tests;

public sealed class SiteContentTests
{
    [Fact]
    public async Task Content_UsesCurrentSiteRolesAndResources()
    {
        var provider = new MutableProvider();
        using var factory = new PortalWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPortalProvider>();
                services.AddSingleton<IPortalProvider>(provider);
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "test";
                    options.DefaultChallengeScheme = "test";
                    options.DefaultForbidScheme = "test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("test", _ => { });
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/home/content/file.txt")).StatusCode);
        provider.Sites = [new SiteInfo { Id = "home", Name = "Home", Content = new TestFileProvider() }];
        var response = await client.GetAsync("/home/content/file.txt");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("content", await response.Content.ReadAsStringAsync());
        Assert.False(response.Content.Headers.Contains("Last-Modified"));

        provider.Sites = [new SiteInfo { Id = "home", Name = "Home", Roles = ["media"], Content = new TestFileProvider() }];
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/home/content/file.txt")).StatusCode);
        provider.Sites = [new SiteInfo { Id = "home", Name = "Home", Roles = ["admin"], Content = new TestFileProvider() }];
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/home/content/file.txt")).StatusCode);
        provider.Sites = [];
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/home/content/file.txt")).StatusCode);

        var lastModified = new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero);
        provider.Sites = [new SiteInfo { Id = "home", Name = "Home", Content = new TestFileProvider(lastModified) }];
        response = await client.GetAsync("/home/content/file.txt");
        Assert.Equal(lastModified, response.Content.Headers.LastModified);
    }

    private sealed class MutableProvider : IPortalProvider
    {
        public IReadOnlyList<SiteInfo> Sites { get; set; } = [];
        public ValueTask<PortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PortalDetails("Portal") { Sites = Sites });
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "user"), new Claim(ClaimTypes.Role, "media")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class TestFileProvider(DateTimeOffset? lastModified = null) : IFileProvider
    {
        public IFileInfo GetFileInfo(string subpath) => subpath == "file.txt" ? new TestFileInfo(lastModified) : new NotFoundFileInfo(subpath);
        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;
        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    private sealed class TestFileInfo(DateTimeOffset? lastModified) : IFileInfo
    {
        public bool Exists => true;
        public long Length => 7;
        public string? PhysicalPath => null;
        public string Name => "file.txt";
        public DateTimeOffset LastModified => lastModified ?? DateTimeOffset.MinValue;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => new MemoryStream(Encoding.UTF8.GetBytes("content"));
    }
}
