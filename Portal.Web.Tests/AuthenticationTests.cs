using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Portal.Web.Services;
using Xunit;

namespace Portal.Web.Tests;

public sealed class AuthenticationTests : IClassFixture<PortalWebApplicationFactory>
{
    private readonly PortalWebApplicationFactory _factory;

    public AuthenticationTests(PortalWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Home_ChallengesWithOidc()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://identity.test/authorize", response.Headers.Location?.AbsoluteUri);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("portal-web", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        Assert.Contains("groups", query["scope"].ToString().Split(' '));
        Assert.Equal("http://localhost/signin-oidc", query["redirect_uri"]);
    }

    [Fact]
    public async Task SignedOut_IsAnonymous()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/auth/signed-out");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Signed out", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void GroupsClaim_IsUsedForRoles()
    {
        var identity = new ClaimsIdentity(
            [new Claim("groups", "media")],
            authenticationType: "oidc",
            nameType: "name",
            roleType: "groups");

        Assert.True(new ClaimsPrincipal(identity).IsInRole("media"));
    }
}

public sealed class PortalWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Oidc:Authority", "https://identity.test");
        builder.UseSetting("Oidc:ClientId", "portal-web");
        builder.UseSetting("Oidc:ClientSecret", "test-secret");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPortalProvider>();
            services.AddSingleton<IPortalProvider>(new CompositePortalProvider());
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration
                {
                    Issuer = "https://identity.test",
                    AuthorizationEndpoint = "https://identity.test/authorize",
                    TokenEndpoint = "https://identity.test/token"
                });
            });
        });
    }
}