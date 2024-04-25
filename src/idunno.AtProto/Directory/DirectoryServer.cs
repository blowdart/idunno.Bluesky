// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Directory
{
    /// <summary>
    /// Provides a class for sending requests to and receiving responses from an directory service, identified by its service URI.
    /// </summary>
    internal class DirectoryServer
    {
        // https://web.plc.directory/api/redoc#operation/ResolveDid

        /// <summary>
        ///Resolves the specified <paramref name="did"/> on the specified <paramref name="directoryServer"/>.
        /// </summary>
        /// <param name="did">The DID to resolve.</param>
        /// <param name="directoryServer">The directory server to use to resolve the <paramref name="did"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping a <see cref="DidDocument"/> containing the DID resolution results or any error details returned by the service.
        /// </returns>
        public static async Task<HttpResult<DidDocument>> ResolveDid(Did did, Uri directoryServer, HttpClientHandler? httpClientHandler, CancellationToken cancellationToken)
        {
            AtProtoRequest<DidDocument> request = new();

            return await request.Get(
                directoryServer,
                $"/{did}",
                accessToken: null,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
