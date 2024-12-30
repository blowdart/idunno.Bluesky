// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.IdentityModel.Tokens;

using IdentityModel.OidcClient;

namespace idunno.AtProto.Auth
{
    /// <summary>
    /// Helper methods for oauth authentication.
    /// </summary>
    public static class OAuthHelper
    {
        const string OAuthDiscoveryDocumentEndpoint = ".well-known/oauth-authorization-server";

        /// <summary>
        /// Generates a new DPop key
        /// </summary>
        /// <returns></returns>
        public static string GenerateDPopKey()
        {
            using (RSA rsa = RSA.Create(2048))
            {
                RsaSecurityKey rsaKey = new(rsa);
                JsonWebKey jwkKey = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
                jwkKey.Alg = "PS256";
                return JsonSerializer.Serialize(jwkKey);
            }
        }

        /// <summary>
        /// Gets the OAuth authorization URI for starting the OAuth flow.
        /// </summary>
        /// <param name="clientId">The client ID</param>
        /// <param name="redirectUri">The redirect URI where the oauth server should send tokens back to.</param>
        /// <param name="authorizationServer">The authorization server to use.</param>
        /// <param name="scopes">A collection of scopes to request.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="redirectUri"/>, <paramref name="authorizationServer"/> or <paramref name="scopes"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scopes"/> is empty.</exception>
        /// <exception cref="OAuth2Exception">Thrown when the authorize state cannot be prepared or encounters an error during preparation.</exception>
        public static async Task<Uri> GetOAuthAuthorizationUri(
            string clientId,
            Uri redirectUri,
            Uri authorizationServer,
            IEnumerable<string> scopes,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(clientId);
            ArgumentNullException.ThrowIfNull(authorizationServer);
            ArgumentNullException.ThrowIfNull(redirectUri);
            ArgumentNullException.ThrowIfNull(scopes);
            ArgumentOutOfRangeException.ThrowIfZero(scopes.Count());

            var oidcOptions = new OidcClientOptions
            {
                ClientId = clientId,
                Authority = authorizationServer.ToString(),
                Scope = string.Join(" ", scopes.Where(s => !string.IsNullOrEmpty(s))),
                RedirectUri = redirectUri.ToString(),
                LoadProfile = false,
                
            };

            oidcOptions.Policy.Discovery.DiscoveryDocumentPath = OAuthDiscoveryDocumentEndpoint;

            var oidcClient = new OidcClient(oidcOptions);

            AuthorizeState state = await oidcClient.PrepareLoginAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (state is null)
            {
                throw new OAuth2Exception("state preparation failed");
            }
            else if (state.IsError)
            {
                throw new OAuth2Exception(state.Error);
            }
            else
            {
                return new Uri(state.StartUrl);
            }
        }
    }
}
