// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Identity;

namespace idunno.AtProto
{
    /// <summary>
    /// Represents an atproto server and provides methods to send messages and receive responses from the server.
    /// </summary>
    internal partial class AtProtoServer
    {
        // https://docs.bsky.app/docs/api/com-atproto-identity-resolve-handle
        internal const string ResolveHandleEndpoint = "/xrpc/com.atproto.identity.resolveHandle";

        /// <summary>
        /// Resolves a handle (domain name) to a DID.
        /// </summary>
        /// <param name="handle">The handle to resolve.</param>
        /// <param name="service">The service used to resolve the <paramref name="handle"/>.</param>
        /// <param name="accessToken">The access token to authenticate against the <paramref name="service"/> with.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping a resolved <see cref="Did"/> containing the resolution results ,
        /// or any error details returned by the <paramref name="service"/>.
        /// </returns>
        public static async Task<HttpResult<Did>> ResolveHandle(string handle, Uri service, string? accessToken, HttpClientHandler? httpClientHandler, CancellationToken cancellationToken)
        {
            AtProtoHttpClient<ResolveHandleResult> request = new();

            HttpResult<ResolveHandleResult> resolveHandleResult = await request.Get(
                service,
                $"{ResolveHandleEndpoint}?handle={handle}",
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);

            HttpResult<Did> returnValue = new()
            {
                StatusCode = resolveHandleResult.StatusCode,
                Error = resolveHandleResult.Error
            };

            if (resolveHandleResult.Succeeded && resolveHandleResult.Result is not null)
            {
                returnValue.Result = resolveHandleResult.Result.Did;
            }

            return returnValue;
        }
    }
}
