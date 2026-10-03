// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Claims;
using System.Text;

using idunno.AtProto;
using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Samples.ReactBff;

internal static class BffEndpoints
{
    internal const string CookieScheme = "Bff";
    internal const string SessionClaim = "bff-session";

    internal static void MapBffEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.AddEndpointFilter(async (invocation, next) =>
        {
            HttpContext context = invocation.HttpContext;
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                try
                {
                    await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException)
                {
                    return Results.Json(new ApiError("Invalid CSRF token. Reload the page."), statusCode: StatusCodes.Status400BadRequest);
                }
            }

            return await next(invocation);
        });

        api.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            new CsrfResponse(antiforgery.GetAndStoreTokens(context).RequestToken!));

        api.MapPost("/login", async (LoginRequest input, HttpContext context, BlueskySignInManager manager) =>
        {
            if (!Handle.TryParse(input.Handle, out Handle? handle))
            {
                return Results.Json(new ApiError("Enter a valid Bluesky handle."), statusCode: StatusCodes.Status400BadRequest);
            }

            if (context.User.Identity?.IsAuthenticated is true)
            {
                return Results.Json(new ApiError("Log out before starting another login."), statusCode: StatusCodes.Status409Conflict);
            }

            Uri uri = await manager.CreateRedirectUri(handle, cancellationToken: context.RequestAborted);
            return Results.Ok(new LoginResponse(uri.AbsoluteUri));
        });

        app.MapGet("/oauth/callback", async (
            HttpContext context,
            BlueskySignInManager manager,
            BffSessions sessions,
            IOptions<BlueskyAgentOptions> options,
            IHttpClientFactory httpClientFactory,
            TimeProvider clock,
            ILoggerFactory loggerFactory) =>
        {
            var state = await manager.LoadState();
            if (state is null || !context.Request.QueryString.HasValue)
            {
                return Results.Redirect("/?loginError=1");
            }

            var agent = new BlueskyAgent(httpClientFactory: httpClientFactory, options: options.Value);
            bool transferred = false;
            try
            {
                var oauthClient = agent.CreateOAuthClient(state);
                if (!await agent.ProcessOAuth2LoginResponse(oauthClient, context.Request.QueryString.Value[1..], context.RequestAborted))
                {
                    return Results.Redirect("/?loginError=1");
                }

                await WithSession(context, sessions, refresh: false, allowAnonymous: true, async previous =>
                {
                    if (previous is not null)
                    {
                        try
                        {
                            string previousId = context.User.FindFirstValue(SessionClaim)!;
                            await RevokeAndRemovePreviousSessionAsync(
                                sessions,
                                previousId,
                                previous,
                                loggerFactory.CreateLogger("Samples.ReactBff.BffEndpoints"));
                        }
                        finally
                        {
                            await context.SignOutAsync(CookieScheme);
                        }
                    }

                    return Results.NoContent();
                });

                // A callback always receives a fresh random session identifier, never one supplied by the browser.
                string id = sessions.Add(new BlueskySessionClient(agent));
                transferred = true;
                BffSession session = sessions.Find(id)!;
                try
                {
                    await context.SignInAsync(CookieScheme, new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(System.Security.Claims.ClaimTypes.NameIdentifier, agent.Did!.Value), new Claim(SessionClaim, id)], CookieScheme)),
                        new AuthenticationProperties
                        {
                            IsPersistent = false,
                            AllowRefresh = false,
                            IssuedUtc = clock.GetUtcNow(),
                            ExpiresUtc = session.ExpiresAt
                        });
                }
                catch
                {
                    sessions.Remove(id, session);
                    throw;
                }

                return Results.Redirect("/");
            }
            finally
            {
                if (!transferred)
                {
                    agent.Dispose();
                }
            }
        });

        api.MapGet("/session", (HttpContext context, BffSessions sessions) =>
            WithSession(context, sessions, refresh: false, allowAnonymous: true, session => Task.FromResult<IResult>(
                Results.Ok(session is null
                    ? new SessionResponse(false, null, null, null)
                    : new SessionResponse(true, session.Client.Did, session.ExpiresAt, session.CreatedPost)))));

        api.MapGet("/timeline", (HttpContext context, BffSessions sessions) =>
            WithSession(context, sessions, refresh: true, allowAnonymous: false, async session =>
                Results.Ok(await session!.Client.GetTimelineAsync(context.RequestAborted))));

        api.MapPost("/posts", (PostRequest input, HttpContext context, BffSessions sessions) =>
            WithSession(context, sessions, refresh: true, allowAnonymous: false, async session =>
            {
                if (string.IsNullOrWhiteSpace(input.Text) ||
                    Encoding.UTF8.GetByteCount(input.Text) > 3000 ||
                    StringInfo.ParseCombiningCharacters(input.Text).Length > 300)
                {
                    return Results.Json(new ApiError("Posts must contain 1-300 graphemes and at most 3000 UTF-8 bytes."), statusCode: StatusCodes.Status400BadRequest);
                }

                if (session!.PostReference is not null)
                {
                    return Results.Json(new ApiError("Delete the sample's previous post before creating another."), statusCode: StatusCodes.Status409Conflict);
                }

                var created = await session.Client.CreatePostAsync(input.Text, context.RequestAborted);
                if (created.Uri.Authority.Value != session.Client.Did || created.Uri.Collection != CollectionNsid.Post || created.Uri.RecordKey is null)
                {
                    throw new BffException(StatusCodes.Status502BadGateway, "The server returned an unexpected post reference. Check your account manually.");
                }

                session.PostReference = created;
                session.CreatedPost = new CreatedPost(session.PostReference.Uri.ToString(), input.Text);
                return Results.Ok(session.CreatedPost);
            }));

        // No URI or DID is accepted from the browser. Only this session's successful create result can be deleted.
        api.MapDelete("/posts/created", (HttpContext context, BffSessions sessions) =>
            WithSession(context, sessions, refresh: true, allowAnonymous: false, async session =>
            {
                if (session!.PostReference is null)
                {
                    return Results.Json(new ApiError("This session has no created post to delete."), statusCode: StatusCodes.Status404NotFound);
                }

                await session.Client.DeletePostAsync(session.PostReference, context.RequestAborted);
                session.PostReference = null;
                session.CreatedPost = null;
                return Results.NoContent();
            }));

        api.MapPost("/logout", (HttpContext context, BffSessions sessions) =>
            WithSession(context, sessions, refresh: false, allowAnonymous: true, async session =>
            {
                try
                {
                    if (session is not null)
                    {
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await session.Client.LogoutAsync(timeout.Token);
                    }
                }
                finally
                {
                    if (session is not null)
                    {
                        sessions.Remove(context.User.FindFirstValue(SessionClaim)!, session);
                    }

                    await context.SignOutAsync(CookieScheme);
                }

                return Results.NoContent();
            }));
    }

    internal static async Task RevokeAndRemovePreviousSessionAsync(
        BffSessions sessions,
        string id,
        BffSession session,
        ILogger logger)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await session.Client.LogoutAsync(timeout.Token);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            BffLog.PreviousSessionRevocationFailed(logger);
        }
        finally
        {
            sessions.Remove(id, session);
        }
    }

    private static async Task<IResult> WithSession(
        HttpContext context,
        BffSessions sessions,
        bool refresh,
        bool allowAnonymous,
        Func<BffSession?, Task<IResult>> action)
    {
        string? id = context.User.Identity?.IsAuthenticated is true ? context.User.FindFirstValue(SessionClaim) : null;
        BffSession? session = sessions.Find(id);
        if (session is null)
        {
            return allowAnonymous ? await action(null) : Unauthorized();
        }

        await session.Gate.WaitAsync(context.RequestAborted);
        try
        {
            if (!sessions.IsValid(session))
            {
                if (!session.Closed)
                {
                    sessions.Remove(id!, session);
                }

                await context.SignOutAsync(CookieScheme);
                return allowAnonymous ? await action(null) : Unauthorized();
            }

            if (refresh && session.Client.TokenExpiresAt <= context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().AddMinutes(1))
            {
                // The same agent survives requests, so refresh and DPoP nonce changes remain server-side.
                // The session gate serializes refresh, API writes, logout and expiry cleanup.
                bool refreshed;
                try
                {
                    refreshed = await session.Client.RefreshAsync(context.RequestAborted);
                }
                catch
                {
                    sessions.Remove(id!, session);
                    await context.SignOutAsync(CookieScheme);
                    throw;
                }

                if (!refreshed)
                {
                    sessions.Remove(id!, session);
                    await context.SignOutAsync(CookieScheme);
                    return Unauthorized();
                }
            }

            return await action(session);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private static IResult Unauthorized() =>
        Results.Json(new ApiError("The session has ended. Log in again."), statusCode: StatusCodes.Status401Unauthorized);
}
