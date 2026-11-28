using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Portal.Web;
using Portal.Web.Components;
using Portal.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.DetailedErrors = true;
        }
    });

builder.Services.AddFluentUIComponents();

var oidc = builder.Configuration.GetRequiredSection("Oidc");
var authority = oidc["Authority"] ?? throw new InvalidOperationException("Oidc:Authority is required.");
var clientId = oidc["ClientId"] ?? throw new InvalidOperationException("Oidc:ClientId is required.");
var clientSecret = oidc["ClientSecret"] ?? throw new InvalidOperationException("Oidc:ClientSecret is required.");
var apiAudience = oidc["ApiAudience"] ?? "portal-api";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "portal.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = authority;
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.RequireHttpsMetadata = oidc.GetValue("RequireHttpsMetadata", true);
        options.ResponseType = "code";
        options.UsePkce = true;
        options.MapInboundClaims = false;
        options.SaveTokens = false;
        options.Scope.Add("groups");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "groups"
        };
    });
// @spec AUTH-009
builder.Services
    .AddAuthentication()
    .AddJwtBearer(ClientManifestEndpointExtensions.AuthenticationScheme, options =>
    {
        options.Authority = authority;
        options.Audience = apiAudience;
        options.RequireHttpsMetadata = oidc.GetValue("RequireHttpsMetadata", true);
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            NameClaimType = "name",
            RoleClaimType = "groups"
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";
                context.Response.Headers["WWW-Authenticate"] = "Bearer";
                var body = JsonSerializer.Serialize(
                    new ProblemDetails
                    {
                        Type = "about:blank",
                        Title = "Unauthorized",
                        Status = StatusCodes.Status401Unauthorized
                    },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                await context.Response.WriteAsync(body, context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        ClientManifestEndpointExtensions.AuthorizationPolicy,
        policy => policy
            .AddAuthenticationSchemes(ClientManifestEndpointExtensions.AuthenticationScheme)
            .RequireAuthenticatedUser());
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddOutputCache();
// @spec AUTH-017 AUTH-018 SITE-019
builder.Services.AddOpenApi(options =>
{
    options.ShouldInclude = description => description.RelativePath == "api/manifest";
    options.AddDocumentTransformer((document, _, _) =>
    {
        var components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes.Add("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = $"Access token for the configured API audience ({apiAudience})."
        });

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        };
        if (document.Paths is { } paths)
        {
            foreach (var path in paths.Values)
            {
                if (path.Operations is not { } operations)
                {
                    continue;
                }

                foreach (var operation in operations.Values)
                {
                    operation.Security ??= [];
                    operation.Security.Add(requirement);
                }
            }
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddSingleton<IMarkdownConverter, MarkdownConverter>();
builder.Services.AddSingleton(PortalVersion.FromAssembly(typeof(Program).Assembly));

// Add client browser session service (JavaScript interop abstraction)
builder.Services.AddClientBrowserSession();

// Add site click tracking service (uses localStorage)
builder.Services.AddScoped<ISiteClickTracker, LocalStorageSiteClickTracker>();

builder.Services.AddDataProtection()
    .SetApplicationName("Swick.Portal.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "swick.portal.web", "keys")));

builder.AddPortalProvider(portal => portal.AddFile().AddKubernetes());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UsePortalVersionHeader();
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets()
    .AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(context);
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/auth/signed-out");
})
    .RequireAuthorization();

app.MapSiteContentEndpoints();
// @spec AUTH-008 AUTH-009 AUTH-010 AUTH-011 AUTH-012 AUTH-013 AUTH-014 AUTH-015 AUTH-016 SITE-017
app.MapClientManifestEndpoint();
app.MapNativeOidcMetadataEndpoint(app.Configuration);

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    // @spec AUTH-017 AUTH-018 SITE-019
    app.MapOpenApi().RequireAuthorization();
}

app.Run();

public partial class Program;
