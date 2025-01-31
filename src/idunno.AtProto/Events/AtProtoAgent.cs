// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Events;

namespace idunno.AtProto
{
    public partial class AtProtoAgent
    {
        /// <summary>
        /// Raised when this instance of <see cref="AtProtoAgent"/> authenticates to a service.
        /// </summary>
        public event EventHandler<AuthenticatedEventArgs>? Authenticated;

        /// <summary>
        /// Raised when the credentials for this instance of <see cref="AtProtoAgent"/> are refreshed.
        /// </summary>
        public event EventHandler<CredentialsUpdatedEventArgs>? CredentialsUpdated;

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
}
