using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
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
        return builder;

    }

    public static void UseTestUser(this IApplicationBuilder app)
    {
        var config = app.ApplicationServices.GetRequiredService<IConfiguration>();

        if (config["TEST_USER"] is { } testUser && config["TEST_ROLES"] is { } testRoles)
        {
            var options = app.ApplicationServices.GetRequiredService<IOptionsMonitor<TrustedHeaderAuthenticationOptions>>().Get(TrustedHeaderAuth.DefaultScheme);

            app.Use((ctx, next) =>
            {
                ctx.Request.Headers[options.UsernameHeader] = testUser;
                ctx.Request.Headers[options.RoleHeader] = testRoles.Split(',');

                return next(ctx);
            });
        }
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
}

internal sealed class TrustedHeaderAuthenticationOptions : AuthenticationSchemeOptions
{
    public string UsernameHeader { get; set; } = "REMOTE_USER";

    public string RoleHeader { get; set; } = "REMOTE_ROLES";
}