using Portal.Web.Components;
using Portal.Web.Services;
using Microsoft.FluentUI.AspNetCore.Components;
using Portal.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

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

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddOutputCache();

builder.Services.AddSingleton<IMarkdownConverter, MarkdownConverter>();

// Add client browser session service (JavaScript interop abstraction)
builder.Services.AddClientBrowserSession();

// Add site click tracking service (uses localStorage)
builder.Services.AddScoped<ISiteClickTracker, LocalStorageSiteClickTracker>();

builder.Services.AddDataProtection()
    .SetApplicationName("Swick.Portal.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "swick.portal.web", "keys")));

builder.AddPortalProvider();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
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

app.MapDefaultEndpoints();

app.Run();

public partial class Program;
