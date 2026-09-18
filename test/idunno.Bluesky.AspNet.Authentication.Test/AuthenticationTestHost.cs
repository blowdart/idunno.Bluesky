// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// Stands up <see cref="BlueskyAuthenticationHandler"/> and <see cref="BlueskySignInManager"/> inside a real request
/// pipeline, so the tests exercise them the way an application does rather than by calling protected members.
/// </summary>
/// <remarks>
/// <para>
///   The handler is reachable only through the authentication middleware, and most of its interesting behaviour is in
///   what it writes to the response, cookies, status codes and redirects, none of which are observable from a unit
///   test which calls into it directly.
/// </para>
/// <para>
///   Nothing here talks to a PDS. Every path covered by these tests either fails before the handler reaches the
///   network, or is fed credentials from a stub store, so the tests stay hermetic.
/// </para>
/// </remarks>
internal sealed class AuthenticationTestHost : IAsyncDisposable
{
    /// <summary>
    /// The path the challenge redirects unauthenticated requests to.
    /// </summary>
    internal const string LoginPath = "/account/login";

    /// <summary>
    /// The path a sign out is expected to be issued from.
    /// </summary>
    internal const string LogoutPath = "/account/logout";

    /// <summary>
    /// The scheme every test in this harness authenticates against.
    /// </summary>
    internal const string Scheme = "Bluesky";

    /// <summary>
    /// The name the handler writes its authentication cookie under, derived from the scheme name the same way
    /// <see cref="PostConfigureBlueskyAuthenticationOptions"/> derives it.
    /// </summary>
    internal const string CookieName = ".Bluesky." + Scheme;

    private readonly IHost _host;
    private readonly PendingSignIn _pendingSignIn;

    private AuthenticationTestHost(IHost host, PendingSignIn pendingSignIn)
    {
        _host = host;
        _pendingSignIn = pendingSignIn;
        Client = host.GetTestClient();
        Client.BaseAddress = new Uri("https://localhost/");
    }

    /// <summary>
    /// Carries the identity the sign in endpoints should sign in. The test server does not flow the caller's execution
    /// context into the request, so this is held by the host instance and captured by the endpoint delegates rather
    /// than passed through an <see cref="AsyncLocal{T}"/>.
    /// </summary>
    private sealed class PendingSignIn
    {
        internal ClaimsIdentity? Identity { get; set; }

        internal DateTimeOffset? ExpiresUtc { get; set; }
    }

    /// <summary>
    /// A client whose requests run through the authentication middleware. It does not follow redirects, because the
    /// redirect a handler writes is usually the thing under test.
    /// </summary>
    internal HttpClient Client { get; }

    /// <summary>
    /// The <see cref="IMeterFactory"/> the handler records against, so a test can collect its measurements.
    /// </summary>
    internal IMeterFactory MeterFactory => _host.Services.GetRequiredService<IMeterFactory>();

    /// <summary>
    /// The identity store the configured scheme is using.
    /// </summary>
    internal IIdentityStore IdentityStore { get; private set; } = default!;

    /// <summary>
    /// The options the configured scheme was built with, so a test can reach the data protection provider and cookie
    /// builder the sign in manager is using.
    /// </summary>
    internal BlueskyAuthenticationOptions Options { get; private set; } = default!;

    /// <summary>
    /// The protector the sign in manager writes and reads correlation cookies with, so a test can forge one.
    /// </summary>
    internal IDataProtector CorrelationProtector =>
        Options.DataProtectionProvider!.CreateProtector(Constants.CorrelationPurpose, "v2");

    /// <summary>
    /// Writes a correlation cookie value in the format the sign in manager reads, so a test can present one whose
    /// expiry has already passed without waiting for it.
    /// </summary>
    internal string ForgeCorrelationCookie(Guid correlationId, DateTimeOffset expiration) =>
        CorrelationProtector.Protect(
            string.Create(CultureInfo.InvariantCulture, $"{correlationId:D}|{expiration.ToUnixTimeSeconds()}"));

    /// <summary>
    /// Writes a correlation cookie which the sign in manager can unprotect but cannot parse.
    /// </summary>
    internal string ForgeMalformedCorrelationCookie() => CorrelationProtector.Protect("this is not a correlation id");

    internal static async Task<AuthenticationTestHost> Create(
        IIdentityStore? identityStore = null,
        Action<BlueskyAuthenticationOptions>? configureOptions = null,
        bool authenticateByDefault = true,
        IHttpClientFactory? httpClientFactory = null)
    {
        PendingSignIn pendingSignIn = new();

        IHostBuilder hostBuilder = new HostBuilder().ConfigureWebHost(webHostBuilder =>
        {
            webHostBuilder
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddMetrics();
                    services.AddRouting();
                    services.AddDataProtection();

                    services.Configure<BlueskyAgentOptions>(options =>
                        options.OAuthOptions = new OAuthOptions("https://localhost/client-metadata.json")
                        {
                            ReturnUri = new Uri("https://localhost/signin-bluesky")
                        });

                    // With no default authenticate scheme, and no authentication middleware, nothing reads the cookie
                    // before an endpoint runs.
                    AuthenticationBuilder authenticationBuilder = authenticateByDefault
                        ? services.AddAuthentication(Scheme)
                        : services.AddAuthentication();

                    authenticationBuilder.AddBluesky(Scheme, options =>
                    {
                        options.LoginPath = LoginPath;
                        options.LogoutPath = LogoutPath;

                        if (identityStore is not null)
                        {
                            options.IdentityStore = identityStore;
                        }

                        configureOptions?.Invoke(options);
                    });

                    services.AddBlueskyAgentFactory(Scheme);

                    // Registered after AddBluesky so it replaces the SSRF protected client the package registers, which
                    // would otherwise try to reach the real network when an agent revokes credentials.
                    if (httpClientFactory is not null)
                    {
                        services.AddSingleton(httpClientFactory);
                    }
                })
                .Configure(app =>
                {
                    // Without the authentication middleware nothing authenticates a request before it reaches an
                    // endpoint, which is how an application reaches a sign out endpoint the handler has never read a
                    // cookie for. The endpoints below call AuthenticateAsync, SignInAsync and SignOutAsync explicitly,
                    // so they work either way.
                    if (authenticateByDefault)
                    {
                        app.UseAuthentication();
                    }

                    app.UseRouting();
                    app.UseEndpoints(endpoints => MapEndpoints(endpoints, pendingSignIn));
                });
        });

        IHost host = await hostBuilder.StartAsync();

        var testHost = new AuthenticationTestHost(host, pendingSignIn);

        testHost.Options = host.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<BlueskyAuthenticationOptions>>()
            .Get(Scheme);

        testHost.IdentityStore = testHost.Options.IdentityStore!;

        return testHost;
    }

    /// <summary>
    /// Signs an identity in through the handler and returns the raw response, so a test can inspect the cookie the
    /// handler wrote and the headers it set alongside it.
    /// </summary>
    /// <param name="identity">The identity to sign in.</param>
    /// <param name="expiresUtc">When the ticket should expire. Defaults to the handler's own expiry.</param>
    internal async Task<HttpResponseMessage> SignIn(ClaimsIdentity identity, DateTimeOffset? expiresUtc = null)
    {
        _pendingSignIn.Identity = identity;
        _pendingSignIn.ExpiresUtc = expiresUtc;

        try
        {
            return await Client.GetAsync(new Uri("/test/signin", UriKind.Relative), TestContext.Current.CancellationToken);
        }
        finally
        {
            _pendingSignIn.Identity = null;
            _pendingSignIn.ExpiresUtc = null;
        }
    }

    /// <summary>
    /// Signs an identity in through the handler and returns the authentication cookie it wrote.
    /// </summary>
    /// <param name="identity">The identity to sign in.</param>
    /// <param name="expiresUtc">When the ticket should expire. Defaults to the handler's own expiry.</param>
    internal async Task<string> SignInAndCaptureCookie(ClaimsIdentity identity, DateTimeOffset? expiresUtc = null)
    {
        using HttpResponseMessage response = await SignIn(identity, expiresUtc);

        return ExtractCookie(response, CookieName)
            ?? throw new InvalidOperationException($"Sign in did not write a {CookieName} cookie.");
    }

    /// <summary>
    /// Signs an identity in through the handler whilst the request carries an existing authentication cookie, which is
    /// what an application does when a user signs in again without signing out first.
    /// </summary>
    /// <param name="identity">The identity to sign in.</param>
    /// <param name="cookie">The authentication cookie the request should carry.</param>
    internal async Task<HttpResponseMessage> SignInCarryingCookie(ClaimsIdentity identity, string cookie)
    {
        _pendingSignIn.Identity = identity;

        try
        {
            return await GetWithCookie("/test/signin", cookie);
        }
        finally
        {
            _pendingSignIn.Identity = null;
        }
    }

    /// <summary>
    /// Issues a request to <paramref name="path"/> carrying <paramref name="cookie"/> as the authentication cookie.
    /// </summary>
    internal async Task<HttpResponseMessage> GetWithCookie(string path, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={cookie}");
        }

        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Issues a request to <paramref name="path"/> carrying <paramref name="value"/> as the correlation cookie.
    /// </summary>
    internal async Task<HttpResponseMessage> GetWithCorrelationCookie(string path, string? value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));

        if (value is not null)
        {
            request.Headers.Add("Cookie", $"{Constants.CorrelationCookieName}={value}");
        }

        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reads the value of <paramref name="name"/> out of the Set-Cookie headers on <paramref name="response"/>,
    /// or <see langword="null"/> if the response did not set it.
    /// </summary>
    internal static string? ExtractCookie(HttpResponseMessage response, string name)
    {
        ArgumentNullException.ThrowIfNull(response);

        return SetCookieHeader(response, name) is string header
            ? header.Split(';')[0].Split('=', 2)[1]
            : null;
    }

    /// <summary>
    /// Returns the whole Set-Cookie header for <paramref name="name"/>, so a test can assert on its attributes.
    /// </summary>
    internal static string? SetCookieHeader(HttpResponseMessage response, string name)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookieHeaders))
        {
            return null;
        }

        return setCookieHeaders.FirstOrDefault(header => header.StartsWith($"{name}=", StringComparison.Ordinal));
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>
    /// Signs an identity in from the configured login path, carrying <paramref name="returnUrl"/> as the return URL
    /// parameter, and returns the response so a test can inspect where the handler decided to redirect to.
    /// </summary>
    internal async Task<HttpResponseMessage> SignInFromLoginPath(ClaimsIdentity identity, string returnUrl)
    {
        _pendingSignIn.Identity = identity;

        try
        {
            return await Client.GetAsync(
                new Uri($"{LoginPath}?ReturnUrl={Uri.EscapeDataString(returnUrl)}", UriKind.Relative),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            _pendingSignIn.Identity = null;
        }
    }

    private static void MapEndpoints(IEndpointRouteBuilder endpoints, PendingSignIn pendingSignIn)
    {
        // Reports what the handler made of the request, so a test can assert on the authentication outcome rather than
        // on a proxy for it.
        endpoints.MapGet("/test/authenticate", async context =>
        {
            AuthenticateResult result = await context.AuthenticateAsync(Scheme);

            context.Response.StatusCode = StatusCodes.Status200OK;

            string did = result.Succeeded
                ? result.Principal?.FindFirst(AtProtoClaims.Did)?.Value ?? string.Empty
                : string.Empty;

            await context.Response.WriteAsync(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"succeeded={result.Succeeded};none={result.None};failure={result.Failure?.Message};did={did}"));
        });

        endpoints.MapGet("/test/challenge", context => context.ChallengeAsync(Scheme));

        // Reports which identity store the agent factory wired an agent's credential updates to, for a user whose
        // identity carries the authentication type named in the query string.
        endpoints.MapGet("/test/agent/store", async context =>
        {
            string authenticationType = context.Request.Query["scheme"].ToString();

            context.User = new ClaimsPrincipal(
                TestData.AuthenticatedClaimsIdentity(TestData.NewDid(), authenticationType: authenticationType));

            BlueskyAgentFactory factory = context.RequestServices.GetRequiredService<BlueskyAgentFactory>();

            using BlueskyAgent agent = factory.CreateAgent();

            BlueskyAuthenticationOptions options = context.RequestServices
                .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<BlueskyAuthenticationOptions>>()
                .Get(Scheme);

            await context.Response.WriteAsync(
                ReferenceEquals(agent.CredentialsUpdatedAsync?.Target, options.IdentityStore)
                    ? "store=scheme"
                    : "store=other");
        });

        endpoints.MapGet("/test/signin", async context =>
        {
            ClaimsIdentity identity = pendingSignIn.Identity
                ?? throw new InvalidOperationException("No identity was set for the sign in endpoint.");

            await context.SignInAsync(
                Scheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    IsPersistent = true,
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = pendingSignIn.ExpiresUtc
                });
        });

        // Sign out is mapped on the configured logout path as well as a neutral one, because the handler only honours
        // the return URL parameter when the request came in on the logout path.
        endpoints.MapGet(LogoutPath, context => context.SignOutAsync(Scheme));
        endpoints.MapGet("/test/signout", context => context.SignOutAsync(Scheme));

        // Signing in on the login path is what an OAuth callback does, and is the only place the handler honours the
        // return URL parameter.
        endpoints.MapGet(LoginPath, async context =>
        {
            if (pendingSignIn.Identity is not ClaimsIdentity identity)
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                await context.Response.WriteAsync("login page");
                return;
            }

            await context.SignInAsync(
                Scheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    IsPersistent = true,
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = pendingSignIn.ExpiresUtc
                });
        });

        // The correlation endpoints drive BlueskySignInManager directly. They stop short of processing an OAuth
        // response, which would need an authorization server, and cover the correlation state handling which runs
        // before that point.
        endpoints.MapGet("/test/correlation/create", async context =>
        {
            BlueskySignInManager signInManager = context.RequestServices.GetRequiredService<BlueskySignInManager>();
            Guid correlationId = Guid.NewGuid();

            await signInManager.SaveStateAndCreateCorrelationCookie(
                TestData.LoginState(correlationId),
                correlationId,
                markCookieAsSecure: !context.Request.Query.ContainsKey("insecure"));

            await context.Response.WriteAsync(correlationId.ToString());
        });

        endpoints.MapGet("/test/correlation/load", async context =>
        {
            BlueskySignInManager signInManager = context.RequestServices.GetRequiredService<BlueskySignInManager>();

            try
            {
                OAuthLoginState? state = await signInManager.LoadState();

                await context.Response.WriteAsync(state is null ? "state=null" : "state=found");
            }
            catch (InvalidOperationException ex)
            {
                await context.Response.WriteAsync($"threw={ex.Message}");
            }
        });

        endpoints.MapGet("/test/callback", async context =>
        {
            BlueskySignInManager signInManager = context.RequestServices.GetRequiredService<BlueskySignInManager>();

            SignInResult result = await signInManager.SignIn();

            await context.Response.WriteAsync(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"succeeded={result.Succeeded};missingQueryString={result.MissingQueryString};missingCorrelationState={result.MissingCorrelationState}"));
        });
    }
}
