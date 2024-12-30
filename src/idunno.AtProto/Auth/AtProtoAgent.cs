// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

using idunno.AtProto.Auth;

namespace idunno.AtProto
{
    public partial class AtProtoAgent
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static readonly Uri s_defaultAuthorizationServer = new("https://bsky.social");

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static readonly IEnumerable<string> s_defaultScopes = new[] { "atproto" };

        /// <summary>
        /// Gets the OAuth authorization URI for starting the OAuth flow.
        /// </summary>
        /// <param name="clientId">The client ID</param>
        /// <param name="redirectUri">The redirect URI where the oauth server should send tokens back to.</param>
        /// <param name="authorizationServer">The authorization server to use.</param>
        /// <param name="scopes">An optional collection of scopes. Defaults to "atproto".</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="redirectUri"/> is null.</exception>
        /// <exception cref="OAuth2Exception">Thrown when the authorize state cannot be prepared or encounters an error during preparation.</exception>
        public async Task<Uri> GetOAuthAuthorizationUri(
            string clientId,
            Uri redirectUri,
            Uri? authorizationServer = null,
            IEnumerable<string>? scopes = null,
            CancellationToken cancellationToken = default)
        {
            authorizationServer ??= s_defaultAuthorizationServer;
            scopes ??= s_defaultScopes;

            Uri result = await Auth.OAuthHelper.GetOAuthAuthorizationUri(clientId, redirectUri, authorizationServer, scopes, cancellationToken).ConfigureAwait(false);

            Logger.OAuthLoginUriGenerated(_logger, authorizationServer, result);

            return result;
        }
    }
}
