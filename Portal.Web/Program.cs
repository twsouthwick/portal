using Portal.Web.Components;
using Portal.Web.Services;
using Microsoft.FluentUI.AspNetCore.Components;
using Portal.Web;
using Microsoft.AspNetCore.DataProtection;

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

builder.Services.AddAuthentication()
    .AddTrustedHeaderAuth(options => { });

builder.Services.AddOutputCache();

builder.Services.AddSingleton<IMarkdownConverter, MarkdownConverter>();

// Add client browser session service (JavaScript interop abstraction)
builder.Services.AddClientBrowserSession();

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

app.UseHttpsRedirection();
app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapSiteContentEndpoints();

app.MapDefaultEndpoints();

app.Run();
