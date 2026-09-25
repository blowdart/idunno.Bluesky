// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// Events which may be used during the authentication process.
/// </summary>
public class BlueskyAuthenticationEvents
{
    /// <summary>
    /// Invoked to validate the principal.
    /// </summary>
    public Func<BlueskyValidatePrincipalContext, Task> OnValidatePrincipal { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked to check if the cookie should be renewed.
    /// </summary>
    public Func<BlueskySlidingExpirationContext, Task> OnCheckSlidingExpiration { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked on signing in.
    /// </summary>
    public Func<BlueskySigningInContext, Task> OnSigningIn { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked after sign in has completed.
    /// </summary>
    public Func<BlueskySignedInContext, Task> OnSignedIn { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked on signing out.
    /// </summary>
    public Func<BlueskySigningOutContext, Task> OnSigningOut { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked when the client needs to be redirected to the sign in url.
    /// </summary>
    public Func<RedirectContext<BlueskyAuthenticationOptions>, Task> OnRedirectToLogin { get; set; } = context =>
    {
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    /// <summary>
    /// Invoked when the client needs to be redirected to the access denied url.
    /// </summary>
    public Func<RedirectContext<BlueskyAuthenticationOptions>, Task> OnRedirectToAccessDenied { get; set; } = context =>
    {
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    /// <summary>
    /// Invoked when the client is to be redirected to logout.
    /// </summary>
    public Func<RedirectContext<BlueskyAuthenticationOptions>, Task> OnRedirectToLogout { get; set; } = context =>
    {
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    /// <summary>
    /// Invoked when the client is to be redirected after logout.
    /// </summary>
    public Func<RedirectContext<BlueskyAuthenticationOptions>, Task> OnRedirectToReturnUrl { get; set; } = context =>
    {
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    /// <summary>
    /// Invoked to validate the principal.
    /// </summary>
    /// <param name="context">The <see cref="BlueskyValidatePrincipalContext"/>.</param>
    public virtual Task ValidatePrincipal(BlueskyValidatePrincipalContext context) => OnValidatePrincipal(context);

    /// <summary>
    /// Invoked to check if the cookie should be renewed.
    /// </summary>
    /// <param name="context">The <see cref="BlueskySlidingExpirationContext"/>.</param>
    public virtual Task CheckSlidingExpiration(BlueskySlidingExpirationContext context) => OnCheckSlidingExpiration(context);

    /// <summary>
    /// Invoked during sign in.
    /// </summary>
    /// <param name="context">The <see cref="BlueskySigningInContext"/>.</param>
    public virtual Task SigningIn(BlueskySigningInContext context) => OnSigningIn(context);

    /// <summary>
    /// Invoked after sign in has completed.
    /// </summary>
    /// <param name="context">The <see cref="BlueskySignedInContext"/>.</param>
    public virtual Task SignedIn(BlueskySignedInContext context) => OnSignedIn(context);

    /// <summary>
    /// Invoked on sign out.
    /// </summary>
    /// <param name="context">The <see cref="BlueskySigningOutContext"/>.</param>
    public virtual Task SigningOut(BlueskySigningOutContext context) => OnSigningOut(context);

    /// <summary>
    /// Invoked when the client is being redirected to the log out url.
    /// </summary>
    /// <param name="context">The <see cref="RedirectContext{TOptions}"/>.</param>
    public virtual Task RedirectToLogout(RedirectContext<BlueskyAuthenticationOptions> context) => OnRedirectToLogout(context);

    /// <summary>
    /// Invoked when the client is being redirected to the log in url.
    /// </summary>
    /// <param name="context">The <see cref="RedirectContext{TOptions}"/>.</param>
    public virtual Task RedirectToLogin(RedirectContext<BlueskyAuthenticationOptions> context) => OnRedirectToLogin(context);

    /// <summary>
    /// Invoked when the client is being redirected after log out.
    /// </summary>
    /// <param name="context">The <see cref="RedirectContext{TOptions}"/>.</param>
    public virtual Task RedirectToReturnUrl(RedirectContext<BlueskyAuthenticationOptions> context) => OnRedirectToReturnUrl(context);

    /// <summary>
    /// Invoked when the client is being redirected to the access denied url.
    /// </summary>
    /// <param name="context">The <see cref="RedirectContext{TOptions}"/>.</param>
    public virtual Task RedirectToAccessDenied(RedirectContext<BlueskyAuthenticationOptions> context) => OnRedirectToAccessDenied(context);
}
