using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Portal.Web.Services;
using Xunit;

namespace Portal.Web.Tests;

public sealed class ClientManifestEndpointTests
{
    private const string AuthenticationScheme = "ClientManifestBearer";
    private const string Audience = "portal-api";
    private const string Issuer = "https://identity.test";
    private static readonly SymmetricSecurityKey SigningKey =
        new(RandomNumberGenerator.GetBytes(32));

    // @spec AUTH-008 AUTH-009 SITE-017
    [Fact]
    public async Task Manifest_RequiresBearerTokenAndRejectsInvalidTokens()
    {
        using var provider = new MutablePortalProvider(PortalDetails());
        using var factory = CreateFactory(provider);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var missingToken = await client.GetAsync("/api/manifest");

        Assert.Equal(HttpStatusCode.Unauthorized, missingToken.StatusCode);
        Assert.Equal("application/problem+json", missingToken.Content.Headers.ContentType?.MediaType);
        Assert.Contains(missingToken.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
        using var missingBody = JsonDocument.Parse(await missingToken.Content.ReadAsStringAsync());
        Assert.Equal(401, missingBody.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(0, provider.Calls);

        foreach (var token in new[]
        {
            CreateToken("admin", "different-api"),
            CreateToken("admin", issuer: "https://other-identity.test"),
            CreateToken("admin", expires: DateTime.UtcNow.AddMinutes(-10)),
            CreateToken("admin", signingKey: new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)))
        })
        {
            using var invalidRequest = AuthorizedRequest(token);
            var invalidToken = await client.SendAsync(invalidRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, invalidToken.StatusCode);
            Assert.Equal("application/problem+json", invalidToken.Content.Headers.ContentType?.MediaType);
        }

        Assert.Equal(0, provider.Calls);
    }

    // @spec AUTH-010 AUTH-011 AUTH-014
    [Fact]
    public async Task Manifest_UsesBearerRolesAndReturnsOnlyTheClientContract()
    {
        using var provider = new MutablePortalProvider(PortalDetails());
        using var factory = CreateFactory(provider);
        using var client = factory.CreateClient();
        using var request = AuthorizedRequest(CreateToken("media"));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsByteArrayAsync();
        using var manifest = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new EntityTagHeaderValue($"\"{Convert.ToHexString(SHA256.HashData(body))}\""),
            response.Headers.ETag);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.Contains("Authorization", response.Headers.Vary);
        Assert.Contains("Cookie", response.Headers.Vary);
        Assert.True(response.Headers.ETag?.IsWeak is false);
        var sites = manifest.RootElement.GetProperty("sites").EnumerateArray().ToArray();
        Assert.Equal(["public", "media"], sites.Select(site => site.GetProperty("id").GetString()!).ToArray());
        Assert.True(sites[1].GetProperty("isPrivate").GetBoolean());
        Assert.True(sites[1].GetProperty("isInternal").GetBoolean());
        Assert.DoesNotContain("roles", sites[1].ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content", sites[1].ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, provider.Calls);
    }

    // @spec AUTH-012 AUTH-013 AUTH-014
    [Fact]
    public async Task Manifest_EtagsFilteredRepresentationsAndHonorsConditionalRequests()
    {
        using var provider = new MutablePortalProvider(PortalDetails());
        using var factory = CreateFactory(provider);
        using var client = factory.CreateClient();

        using var mediaRequest = AuthorizedRequest(CreateToken("media"));
        var mediaResponse = await client.SendAsync(mediaRequest);
        var mediaTag = Assert.IsType<EntityTagHeaderValue>(mediaResponse.Headers.ETag);

        using var adminRequest = AuthorizedRequest(CreateToken("admin"));
        var adminResponse = await client.SendAsync(adminRequest);
        var adminTag = Assert.IsType<EntityTagHeaderValue>(adminResponse.Headers.ETag);
        Assert.NotEqual(mediaTag, adminTag);

        using var conditionalRequest = AuthorizedRequest(CreateToken("media"));
        conditionalRequest.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(mediaTag.Tag, isWeak: true));
        conditionalRequest.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"other\""));
        var notModified = await client.SendAsync(conditionalRequest);

        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        Assert.Empty(await notModified.Content.ReadAsByteArrayAsync());
        Assert.Equal(mediaTag, notModified.Headers.ETag);
        Assert.True(notModified.Headers.CacheControl?.Private);
        Assert.True(notModified.Headers.CacheControl?.NoCache);

        using var wildcardRequest = AuthorizedRequest(CreateToken("media"));
        wildcardRequest.Headers.TryAddWithoutValidation("If-None-Match", "*");
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(wildcardRequest)).StatusCode);

        provider.Details = PortalDetails("Changed");
        using var changedRequest = AuthorizedRequest(CreateToken("media"));
        changedRequest.Headers.IfNoneMatch.Add(mediaTag);
        var changed = await client.SendAsync(changedRequest);

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.NotEqual(mediaTag, changed.Headers.ETag);
        Assert.Equal(5, provider.Calls);
    }

    // @spec AUTH-015 SITE-017
    [Fact]
    public async Task Manifest_ReportsProviderFailureAsProblemDetails()
    {
        using var provider = new MutablePortalProvider(PortalDetails())
        {
            Failure = new InvalidOperationException("provider unavailable")
        };
        using var factory = CreateFactory(provider);
        using var client = factory.CreateClient();
        using var request = AuthorizedRequest(CreateToken("media"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(503, body.RootElement.GetProperty("status").GetInt32());
    }

    // @spec AUTH-017 AUTH-018 AUTH-019 SITE-019
    [Fact]
    public async Task OpenApi_IsAuthenticatedDevelopmentOnlyAndHasNoInteractiveUi()
    {
        using var developmentProvider = new MutablePortalProvider(PortalDetails());
        using var developmentFactory = CreateFactory(developmentProvider);
        using var developmentClient = developmentFactory.CreateClient();
        using var anonymousClient = developmentFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var unauthorizedDocument = await anonymousClient.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedDocument.StatusCode);

        using var openApiRequest = AuthorizedRequest(CreateToken("media"));
        openApiRequest.RequestUri = new Uri("/openapi/v1.json", UriKind.Relative);

        var documentResponse = await developmentClient.SendAsync(openApiRequest);
        var documentJson = await documentResponse.Content.ReadAsStringAsync();
        Assert.True(documentResponse.StatusCode == HttpStatusCode.OK, documentJson);
        using var document = JsonDocument.Parse(documentJson);
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty("/api/manifest").GetProperty("get");
        var responses = operation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.EndsWith(
            "/ClientManifest",
            responses.GetProperty("200").GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString());
        Assert.True(responses.TryGetProperty("304", out _));
        Assert.True(responses.TryGetProperty("401", out var unauthorized));
        Assert.True(responses.TryGetProperty("503", out var unavailable));
        Assert.True(unauthorized.GetProperty("content").TryGetProperty("application/problem+json", out _));
        Assert.True(unavailable.GetProperty("content").TryGetProperty("application/problem+json", out _));
        var components = document.RootElement.GetProperty("components");
        Assert.Contains("ClientManifest", components
            .GetProperty("schemas").EnumerateObject().Select(schema => schema.Name));
        Assert.Contains(
            components.GetProperty("securitySchemes").EnumerateObject(),
            scheme => scheme.Value.GetProperty("type").GetString() == "http" &&
                scheme.Value.GetProperty("scheme").GetString() == "bearer");
        Assert.True(operation.TryGetProperty("security", out var security));
        Assert.NotEmpty(security.EnumerateArray());
        Assert.Equal(
            ["/api/manifest"],
            paths.EnumerateObject().Select(path => path.Name).ToArray());

        using var productionProvider = new MutablePortalProvider(PortalDetails());
        using var productionFactory = CreateFactory(productionProvider, "Production");
        using var productionClient = productionFactory.CreateClient();
        using var productionRequest = AuthorizedRequest(CreateToken("media"));
        productionRequest.RequestUri = new Uri("/openapi/v1.json", UriKind.Relative);
        var productionResponse = await productionClient.SendAsync(productionRequest);
        Assert.NotEqual("application/json", productionResponse.Content.Headers.ContentType?.MediaType);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        MutablePortalProvider provider,
        string environment = "Development")
    {
        return new ManifestWebApplicationFactory(provider, SigningKey, environment);
    }

    private static HttpRequestMessage AuthorizedRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/manifest");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string CreateToken(
        string role,
        string audience = Audience,
        string issuer = Issuer,
        DateTime? expires = null,
        SymmetricSecurityKey? signingKey = null)
    {
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "test-user",
                ["groups"] = new[] { role }
            },
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.HmacSha256)
        });
        return token;
    }

    private static PortalDetails PortalDetails(string name = "Test Portal") => new(name)
    {
        Description = "Manifest test",
        Sites =
        [
            new SiteInfo { Id = "public", Name = "Public" },
            new SiteInfo { Id = "media", Name = "Media", Roles = ["media"], IsPrivate = true, IsInternal = true },
            new SiteInfo { Id = "admin", Name = "Admin", Roles = ["admin"] }
        ]
    };

    private sealed class ManifestWebApplicationFactory(
        MutablePortalProvider provider,
        SymmetricSecurityKey signingKey,
        string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Oidc:Authority", Issuer);
            builder.UseSetting("Oidc:ClientId", "portal-web");
            builder.UseSetting("Oidc:ClientSecret", "test-secret");
            builder.UseSetting("Oidc:ApiAudience", Audience);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPortalProvider>();
                services.AddSingleton<IPortalProvider>(provider);
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = AuthenticationScheme;
                    options.DefaultChallengeScheme = AuthenticationScheme;
                });
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(signingKey);
                services.PostConfigure<JwtBearerOptions>(AuthenticationScheme, options =>
                {
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        }
    }

    private sealed class MutablePortalProvider(PortalDetails details) : IPortalProvider, IDisposable
    {
        private int _calls;

        public PortalDetails Details { get; set; } = details;

        public Exception? Failure { get; init; }

        public int Calls => Volatile.Read(ref _calls);

        public ValueTask<PortalDetails> GetSiteDetailsAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            if (Failure is not null)
            {
                return ValueTask.FromException<PortalDetails>(Failure);
            }

            return ValueTask.FromResult(Details);
        }

        public void Dispose()
        {
        }
    }
}
