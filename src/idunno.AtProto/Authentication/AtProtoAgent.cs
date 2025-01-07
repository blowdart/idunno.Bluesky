// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.IdentityModel.JsonWebTokens;

using idunno.AtProto.Authentication;
using idunno.AtProto.Events;
using idunno.AtProto.Models;
using idunno.AtProto.Server;

namespace idunno.AtProto
{
    public partial class AtProtoAgent
    {
        /// <summary>
        /// Creates a new instance of <see cref="OAuthClient"/>.
        /// </summary>
        public OAuthClient CreateOAuthClient()
        {
            return new OAuthClient(ConfigureHttpClient, CreateProxyHttpClientHandler, LoggerFactory);
        }

        /// <summary>
        /// Logins into the <paramref name="service"/> with the specified parameters from an OAuth login.
        /// </summary>
        /// <param name="accessToken">The access token to use to build a session.</param>
        /// <param name="refreshToken">The refresh token to use when the access token expires.</param>
        /// <param name="dPoPKey">The key used to sign requests when the authentication type is OAuth.</param>
        /// <param name="dPoPNonce">The key used to sign requests when the authentication type is OAuth.</param>
        /// <param name="service">The service to authenticate to.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="accessToken"/>, <paramref name="refreshToken"/> or <paramref name="service"/> is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
        public async Task<AtProtoHttpResult<bool>> OAuthLogin(string accessToken, string refreshToken, string dPoPKey, string dPoPNonce, Uri service, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(accessToken);
            ArgumentException.ThrowIfNullOrEmpty(refreshToken);
            ArgumentNullException.ThrowIfNull(service);

            JsonWebToken accessJwt = new(accessToken);
            Did did = accessJwt.Subject;

            AtProtoHttpResult<GetSessionResponse> getSessionResult = await GetSession(accessToken, service, cancellationToken).ConfigureAwait(false);

            if (getSessionResult.Succeeded)
            {
                var restoredSession = new Session(service, getSessionResult.Result, accessToken, refreshToken);

                lock (_session_SyncLock)
                {
                    Logger.SessionCreatedFromOauthLogin(_logger, did, service);

                    Session = restoredSession;
                    _sessionRefreshTimer ??= new();
                    StartTokenRefreshTimer();

                    if (restoredSession.AccessJwt is not null && restoredSession.RefreshJwt is not null)
                    {
                        var sessionCreatedEventArgs = new SessionCreatedEventArgs(
                            restoredSession.Did,
                            service,
                            restoredSession.Handle,
                            restoredSession.AccessJwt,
                            restoredSession.RefreshJwt,
                            AuthenticationType.OAuth,
                            dPoPKey: dPoPKey,
                            dPoPNonce: dPoPNonce);

                        OnSessionCreated(sessionCreatedEventArgs);
                    }
                }

                return new AtProtoHttpResult<bool>(true, getSessionResult.StatusCode, getSessionResult.AtErrorDetail, getSessionResult.RateLimit);
            }
            else
            {
                lock (_session_SyncLock)
                {
                    StopTokenRefreshTimer(true);
                }

                Logger.SessionCreatedFromOauthLoginFailed(_logger, did, service, getSessionResult.StatusCode, getSessionResult.AtErrorDetail?.Error, getSessionResult.AtErrorDetail?.Message);

                return new AtProtoHttpResult<bool>(false, getSessionResult.StatusCode, getSessionResult.AtErrorDetail, getSessionResult.RateLimit);
            }
        }

    }
}
