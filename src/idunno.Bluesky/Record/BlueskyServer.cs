// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky.Record;

namespace idunno.Bluesky
{
    public static partial class BlueskyServer
    {

        /// <summary>
        /// Gets the record for the <see cref="Post"/> identified by <paramref name="uri"/> and, optional <paramref name="cid"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the post to return hydrated views for.</param>
        /// <param name="cid">An optional <see cref="Cid"/> of the post to return hydrated views for.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessToken">An optional access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public static async Task<AtProtoHttpResult<PostRecord>> GetPostRecord(
            AtUri uri,
            Cid? cid,
            Uri service,
            string? accessToken,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(uri.RecordKey);

            return await AtProtoServer.GetRecord<PostRecord>(
                uri.Repo,
                CollectionNsid.Post,
                uri.RecordKey,
                cid,
                service,
                accessToken,
                httpClient,
                loggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
