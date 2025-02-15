// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;

using Microsoft.IdentityModel.JsonWebTokens;

namespace idunno.AtProto
{
    internal static class HttpClientExtensions
    {
        public static void AddBearerToken(this HttpRequestMessage httpRequestMessage, string token)
        {
            ArgumentException.ThrowIfNullOrEmpty(token);

            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public static void AddBearerToken(this HttpRequestMessage httpRequestMessage, JsonWebToken token)
        {
            ArgumentNullException.ThrowIfNull(token);

            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.ToString());
        }
    }
}
