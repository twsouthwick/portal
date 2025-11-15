using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
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
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, TestUserStartupFilter>());

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

                if (Context.Request.Headers.TryGetValue(Options.RoleHeader, out var roles))
                {
                    foreach (var role in roles)
                    {
                        if (role is { })
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, role));
                        }
                    }
                }

                var user = new ClaimsPrincipal(identity);

                return AuthenticateResult.Success(new AuthenticationTicket(user, Scheme.Name));
            }

            return AuthenticateResult.NoResult();
        }
    }

    private sealed class TestUserStartupFilter(IConfiguration config, IHostEnvironment env, IOptionsMonitor<TrustedHeaderAuthenticationOptions> trustedHeaders) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => builder =>
        {
            if (env.IsDevelopment())
            {
                if (config["TEST_USER"] is { } testUser && config["TEST_ROLES"] is { } testRoles)
                {
                    var options = trustedHeaders.Get(TrustedHeaderAuth.DefaultScheme);
                    var roles = new StringValues(testRoles.Split(','));
                    builder.Use((ctx, next) =>
                    {
                        ctx.Request.Headers[options.UsernameHeader] = testUser;
                        ctx.Request.Headers[options.RoleHeader] = roles;

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
    public string UsernameHeader { get; set; } = "REMOTE_USER";

    public string RoleHeader { get; set; } = "REMOTE_ROLES";
}