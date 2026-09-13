// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Events;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Raised when this instance of <see cref="AtProtoAgent"/> authenticates to a service.
    /// </summary>
    public event EventHandler<AuthenticatedEventArgs>? Authenticated;

    /// <summary>
    /// Raised when the credentials for this instance of <see cref="AtProtoAgent"/> are refreshed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handlers of this event cannot be awaited. If a handler needs to perform asynchronous work whose failure matters,
    /// such as persisting the updated credentials to a durable store, use <see cref="CredentialsUpdatedAsync"/> instead.
    /// </para>
    /// </remarks>
    public event EventHandler<CredentialsUpdatedEventArgs>? CredentialsUpdated;

    /// <summary>
    /// Gets or sets an asynchronous handler which is awaited when the credentials for this instance of <see cref="AtProtoAgent"/> are refreshed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike the <see cref="CredentialsUpdated"/> event this handler is awaited, so exceptions it throws propagate to the
    /// operation which caused the credentials to be updated, rather than being lost on an unobserved task.
    /// </para>
    /// <para>
    /// AT Proto refresh tokens are single use, so a handler which persists credentials should be wired up here to ensure
    /// a failure to persist is not silently swallowed.
    /// </para>
    /// </remarks>
    public Func<CredentialsUpdatedEventArgs, CancellationToken, Task>? CredentialsUpdatedAsync { get; set; }

    /// <summary>
    /// Raised when the session refresh for this instance of <see cref="AtProtoAgent"/> failed.
    /// </summary>
    public event EventHandler<TokenRefreshFailedEventArgs>? TokenRefreshFailed;

    /// <summary>
    /// Raised when the session for this instance of <see cref="AtProtoAgent"/> ended.
    /// </summary>
    public event EventHandler<UnauthenticatedEventArgs>? Unauthenticated;

    /// <summary>
    /// Called to raise any <see cref="Authenticated"/> events, if any.
    /// </summary>
    /// <param name="e">The <see cref="AuthenticatedEventArgs"/> for the event.</param>
    protected virtual void OnAuthenticated(AuthenticatedEventArgs e)
    {
        EventHandler<AuthenticatedEventArgs>? authenticated = Authenticated;

        if (!_disposed)
        {
            authenticated?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Called to raise any <see cref="CredentialsUpdated"/> events, if any.
    /// </summary>
    /// <param name="e">The <see cref="CredentialsUpdatedEventArgs"/> for the event.</param>
    protected virtual void OnCredentialsUpdated(CredentialsUpdatedEventArgs e)
    {
        EventHandler<CredentialsUpdatedEventArgs>? credentialsUpdated = CredentialsUpdated;

        if (!_disposed)
        {
            credentialsUpdated?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Called to raise any <see cref="CredentialsUpdated"/> events, then await the <see cref="CredentialsUpdatedAsync"/> handler, if any.
    /// </summary>
    /// <param name="e">The <see cref="CredentialsUpdatedEventArgs"/> for the event.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    protected virtual async Task OnCredentialsUpdatedAsync(CredentialsUpdatedEventArgs e, CancellationToken cancellationToken = default)
    {
        OnCredentialsUpdated(e);

        Func<CredentialsUpdatedEventArgs, CancellationToken, Task>? credentialsUpdatedAsync = CredentialsUpdatedAsync;

        if (!_disposed && credentialsUpdatedAsync is not null)
        {
            await credentialsUpdatedAsync(e, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Called to raise any <see cref="TokenRefreshFailed"/> events, if any.
    /// </summary>
    /// <param name="e">The <see cref="TokenRefreshFailedEventArgs"/> for the event.</param>
    protected virtual void OnTokenRefreshFailed(TokenRefreshFailedEventArgs e)
    {
        EventHandler<TokenRefreshFailedEventArgs>? tokenRefreshFailed = TokenRefreshFailed;

        if (!_disposed)
        {
            tokenRefreshFailed?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Called to raise any <see cref="Unauthenticated"/> events, if any.
    /// </summary>
    /// <param name="e">The <see cref="UnauthenticatedEventArgs"/> for the event.</param>
    protected virtual void OnUnauthenticated(UnauthenticatedEventArgs e)
    {
        EventHandler<UnauthenticatedEventArgs>? unauthenticated = Unauthenticated;

        if (!_disposed)
        {
            unauthenticated?.Invoke(this, e);
        }
    }
}
