using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

internal static class TrustedHeaderAuth
{
    public const string DefaultScheme = "TrustedHeader";
}

internal static class TrustedHeaderAuthExtensions
{
    public static AuthenticationBuilder AddTrustedHeaderAuth(this AuthenticationBuilder builder, Action<TrustedHeaderAuthenticationOptions> configure)
    {
        builder.AddScheme<TrustedHeaderAuthenticationOptions, TrustedHeaderAuthenticationHandler>(TrustedHeaderAuth.DefaultScheme, configure);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, TrustedHeaderStartup>());

        return builder;

    }

    private sealed class TrustedHeaderAuthenticationHandler(
        IOptionsMonitor<TrustedHeaderAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<TrustedHeaderAuthenticationOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(HandleAuthenticate());

        private AuthenticateResult HandleAuthenticate()
        {
            if (Context.Request.Headers.TryGetValue(Options.UsernameHeader, out var value) && value is [{ } username])
            {
                var identity = new ClaimsIdentity(
                    authenticationType: "TrustedHeader",
                    nameType: ClaimTypes.Name,
                    roleType: ClaimTypes.Role
                    );

                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, username));

                if (Context.Request.Headers.TryGetValue(Options.NameHeader, out var names) && names.FirstOrDefault() is { } name)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Name, name));
                }
                else
                {
                    identity.AddClaim(new Claim(ClaimTypes.Name, username));
                }

                if (Context.Request.Headers.TryGetValue(Options.RoleHeader, out var roles))
                {
                    foreach (var role in roles)
                    {
                        if (role is { })
                        {
                            foreach (var split in role.Split([',']))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, split));
                            }
                        }
                    }
                }

                var user = new ClaimsPrincipal(identity);

                Logger.LogInformation("User logged on: {User}", user.Identity?.Name);

                return AuthenticateResult.Success(new AuthenticationTicket(user, Scheme.Name));
            }

            return AuthenticateResult.NoResult();
        }
    }

    private sealed class TrustedHeaderStartup(
        IConfiguration config,
        IHostEnvironment env,
        IOptionsMonitor<TrustedHeaderAuthenticationOptions> trustedHeaders,
        ILogger<TrustedHeaderStartup> logger
        ) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => builder =>
        {
            var options = trustedHeaders.Get(TrustedHeaderAuth.DefaultScheme);

            logger.LogInformation("Initializing trusted header authentication: UserHeader={UserHeader}, NameHeader={NameHeader}, RoleHeader={RoleHeader}", options.UsernameHeader, options.NameHeader, options.RoleHeader);

            if (env.IsDevelopment())
            {
                logger.LogWarning("Enabling test user system via headers");

                if (config["TEST_USER"] is { } testUser && config["TEST_ROLES"] is { } testRoles)
                {
                    var roles = new StringValues(testRoles.Split(','));
                    builder.Use((ctx, next) =>
                    {
                        ctx.Request.Headers[options.UsernameHeader] = testUser;
                        ctx.Request.Headers[options.RoleHeader] = roles;
                        ctx.Request.Headers[options.NameHeader] = "Test User";

                        return next(ctx);
                    });
                }
            }

            next(builder);
        };
    }
}

internal sealed class TrustedHeaderAuthenticationOptions : AuthenticationSchemeOptions
{
    public string UsernameHeader { get; set; } = "Remote-User";

    public string RoleHeader { get; set; } = "Remote-Groups";

    public string NameHeader { get; set; } = "Remote-Name";
}