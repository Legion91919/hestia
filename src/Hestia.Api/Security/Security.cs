using System.Security.Claims;
using Agentic.Blocks.Auth;
using Agentic.Blocks.Core;
using Agentic.Blocks.Web;
using Hestia.Investors;
using Hestia.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Hestia.Api.Security;

/// <summary>Is this investor user still allowed in? Fails closed: a tenant user missing from the directory has no access.</summary>
internal static class AccessPolicy
{
    public static async Task<bool> HasAccessAsync(ITenantScopeFactory tenants, string userId, TenantId? tenant)
    {
        if (tenant is null) return true;   // staff accounts have no tenant; roles decide what they can do
        await using var scope = tenants.CreateScope(tenant.Value);
        return scope.ServiceProvider.GetRequiredService<IInvestorUserRepository>().Find(userId)?.HasAccess == true;
    }

    public static Error Revoked => Error.Unauthorized("auth.access_revoked", "Access to Hestia has been withdrawn. Contact investor relations.");
}

/// <summary>
/// Runs on every authenticated request: first the Auth block's security-stamp check, then the revocation check.
/// ASP.NET Core resolves only one <see cref="IClaimsTransformation"/>, so this decorates the block's one.
/// </summary>
internal sealed class RevokedUserGuard(IClaimsTransformation inner, ITenantScopeFactory tenants) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var result = await inner.TransformAsync(principal);
        if (result.Identity?.IsAuthenticated != true || result.GetUserId() is not { } userId) return result;
        return await AccessPolicy.HasAccessAsync(tenants, userId, TenantClaims.FromPrincipal(result)) ? result : new ClaimsPrincipal(new ClaimsIdentity());
    }

    public static void Decorate(IServiceCollection services)
    {
        var original = services.Last(d => d.ServiceType == typeof(IClaimsTransformation));
        services.Remove(original);
        services.AddTransient<IClaimsTransformation>(sp => new RevokedUserGuard(
            (IClaimsTransformation)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!), sp.GetRequiredService<ITenantScopeFactory>()));
    }
}

/// <summary>Filters on the Auth block's endpoints: no self-registration, and revoked users cannot sign in.</summary>
internal static class AuthEndpointGuard
{
    public static async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.HttpContext.Request.Path.Value?.EndsWith("/register", StringComparison.OrdinalIgnoreCase) == true) return Results.NotFound();
        var result = await next(context);
        if (result is SignInHttpResult signIn && signIn.Principal.GetUserId() is { } userId)
        {
            var tenants = context.HttpContext.RequestServices.GetRequiredService<ITenantScopeFactory>();
            if (!await AccessPolicy.HasAccessAsync(tenants, userId, TenantClaims.FromPrincipal(signIn.Principal))) return AccessPolicy.Revoked.ToProblem();
        }

        return result;
    }
}

/// <summary>The session cookie: an HttpOnly, SameSite=Strict cookie that carries the same opaque bearer token API clients use.</summary>
internal static class SessionCookie
{
    public static void ReadTokenFromCookie(BearerTokenOptions options)
    {
        var inner = options.Events.OnMessageReceived;
        options.Events.OnMessageReceived = async ctx =>
        {
            await inner(ctx);
            if (ctx.Token is null && ctx.Request.Headers.Authorization.Count == 0 && ctx.Request.Cookies.TryGetValue(HestiaApp.SessionCookieName, out var token))
            {
                ctx.Token = token;
            }
        };
    }

    public static void Write(HttpContext http, string token, TimeSpan lifetime) =>
        http.Response.Cookies.Append(HestiaApp.SessionCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = lifetime,
            IsEssential = true,
        });

    public static void Delete(HttpContext http) => http.Response.Cookies.Delete(HestiaApp.SessionCookieName, new CookieOptions { Path = "/" });
}

/// <summary>Signs a browser user in: checks the password (Auth block), refuses revoked users, issues a bearer token for the cookie.</summary>
public sealed class SessionService(AuthService auth, ITenantScopeFactory tenants, IOptionsMonitor<BearerTokenOptions> bearer, TimeProvider time)
{
    /// <summary>Returns the token and its lifetime.</summary>
    public async Task<Result<(string Token, TimeSpan Lifetime)>> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var login = await auth.LoginAsync(email, password, cancellationToken);
        if (login.IsFailure) return login.Error;
        if (!await AccessPolicy.HasAccessAsync(tenants, login.Value.Id, TenantId.TryParse(login.Value.TenantId, out var t) ? t : null)) return AccessPolicy.Revoked;

        var options = bearer.Get(BearerTokenDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties { ExpiresUtc = time.GetUtcNow() + options.BearerTokenExpiration };
        var ticket = new AuthenticationTicket(AuthService.CreatePrincipal(login.Value), properties, BearerTokenDefaults.AuthenticationScheme);
        return (options.BearerTokenProtector.Protect(ticket), options.BearerTokenExpiration);
    }
}

/// <summary>Pages redirect signed-out visitors to <c>/login</c>; APIs keep 401/403. Revoked tokens answer 401, not 403.</summary>
internal sealed class BrowserAwareAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly string[] ApiPrefixes = ["/api", "/auth", "/health", "/_blazor", "/account"];
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated != true) authorizeResult = PolicyAuthorizationResult.Challenge();
        var isPage = HttpMethods.IsGet(context.Request.Method) && !ApiPrefixes.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
        if (authorizeResult.Challenged && isPage)
        {
            SessionCookie.Delete(context);
            context.Response.Redirect("/login?returnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
            return Task.CompletedTask;
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
