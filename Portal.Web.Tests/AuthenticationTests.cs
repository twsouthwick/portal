using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
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

    // @spec AUTH-001 AUTH-002 AUTH-016
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

    // @spec AUTH-002
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdToken_GroupsArrayIsUsedForRoles(bool isAdmin)
    {
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var handler = new JsonWebTokenHandler { MapInboundClaims = options.MapInboundClaims };
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "user",
                ["name"] = "Portal User",
                ["groups"] = isAdmin ? new[] { "admin", "media" } : new[] { "media" }
            },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });
        var validation = options.TokenValidationParameters.Clone();
        validation.IssuerSigningKey = key;
        validation.ValidateIssuer = false;
        validation.ValidateAudience = false;

        var result = await handler.ValidateTokenAsync(token, validation);

        Assert.True(result.IsValid, result.Exception?.ToString());
        var principal = new ClaimsPrincipal(result.ClaimsIdentity);
        Assert.True(principal.IsInRole("media"));
        Assert.Equal(isAdmin, principal.IsInRole("admin"));
        Assert.Equal("Portal User", principal.Identity?.Name);
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