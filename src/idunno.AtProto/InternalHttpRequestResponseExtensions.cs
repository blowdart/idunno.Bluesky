// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;

namespace idunno.AtProto
{
    internal static class InternalHttpRequestResponseExtensions
    {
        private const string DPoPNonceHeaderName = "DPoP-Nonce";

        /// <summary>
        /// Checks if the <paramref name="httpResponseHeaders"/> contains a DPoP nonce header.
        /// </summary>
        /// <param name="httpResponseHeaders">The <see cref="HttpRequestHeaders"/> to look through.</param>
        /// <returns><see langword="true"/> if the <paramref name="httpResponseHeaders"/> contains a DPoP nonce header, otherwise <see langword="false"/>.</returns>
        public static bool ContainsDPoPNonce(this HttpResponseHeaders httpResponseHeaders)
        {
            return httpResponseHeaders.Contains(DPoPNonceHeaderName);
        }

        /// <summary>
        /// Returns the DPoP nonce header from the <paramref name="httpResponseHeaders"/> if present, otherwise returns null.
        /// </summary>
        /// <param name="httpResponseHeaders">The <see cref="HttpRequestHeaders"/> to look through.</param>
        /// <returns>The DPoP nonce header from the <paramref name="httpResponseHeaders"/> if present, otherwise null</returns>
        public static string? DPoPNonce(this HttpResponseHeaders httpResponseHeaders)
        {
            if (httpResponseHeaders.TryGetValues(DPoPNonceHeaderName, out IEnumerable<string>? values))
            {
                return values.FirstOrDefault();
            }
            else
            {
                return null;
            }
        }
    }
}
