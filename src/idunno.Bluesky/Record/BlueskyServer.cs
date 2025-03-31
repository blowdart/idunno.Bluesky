// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky.Record;
using idunno.AtProto.Authentication;

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
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PostRecord>> GetPostRecord(
            AtUri uri,
            Cid? cid,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(uri.RecordKey);

            return await AtProtoServer.GetRecord<PostRecord>(
                repo: uri.Repo,
                collection: CollectionNsid.Post,
                rKey: uri.RecordKey,
                cid: cid,
                service : service,
                accessCredentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                loggerFactory: loggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
