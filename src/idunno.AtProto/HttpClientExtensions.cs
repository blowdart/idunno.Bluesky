// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using Microsoft.IdentityModel.JsonWebTokens;

namespace idunno.AtProto
{
    internal static class HttpClientExtensions
    {
        /// <summary>
        /// Adds a bearer token authentication header to the request.
        /// </summary>
        /// <param name="httpRequestMessage">The request message to attach the header to.</param>
        /// <param name="token">The bearer token to attach to the request.</param>
        public static void AddBearerToken(this HttpRequestMessage httpRequestMessage, string token)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(token);

            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public static void AddBearerToken(this HttpRequestMessage httpRequestMessage, JsonWebToken token)
        {
            ArgumentNullException.ThrowIfNull(token);

            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());
        }
    }
}
