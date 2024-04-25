// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.AtProto
{
    /// <summary>
    /// Represents an atproto server and provides methods to send messages and receive responses from the server.
    /// </summary>
    internal partial class AtProtoServer
    {
        // https://docs.bsky.app/docs/api/com-atproto-repo-create-record
        internal const string CreateRecordEndpoint = "/xrpc/com.atproto.repo.createRecord";

        // https://docs.bsky.app/docs/api/com-atproto-repo-create-record
        internal const string DeleteRecordEndpoint = "/xrpc/com.atproto.repo.deleteRecord";

        // https://docs.bsky.app/docs/api/com-atproto-repo-describe-repo
        internal const string DescribeRepoEndpoint = "xrpc/com.atproto.repo.describeRepo";

        /// <summary>
        /// Creates an atproto record in the specified collection.
        /// </summary>
        /// <param name="record">The record to be created.</param>
        /// <param name="collection">The collection the record should be created in.</param>
        /// <param name="creator">The <see cref="Did"/> of the creating actor.</param>
        /// <param name="service">The service to create the record on.</param>
        /// <param name="accessToken">An access token for the specified service.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="RecordResponse"/> containing the ATUri and Cid of the created record, wrapped by an <see cref="HttpResult{T}"/>.</returns>
        public static async Task<HttpResult<StrongReference>> CreateRecord(
            AtProtoRecord record,
            string collection,
            Did creator,
            Uri service,
            string accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            CreateRecordRequest createRecordRequest = new(collection, creator, record);

            AtProtoRequest<StrongReference> request = new();

            return await request.Post(
                service,
                CreateRecordEndpoint,
                createRecordRequest,
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes an atproto record, specified by its rkey, from specified repo/collection.
        /// </summary>
        /// <param name="repo">The handle or Did of the repo to delete from. Typically this is the Did of the account that created the record.</param>
        /// <param name="collection">The collection the record should be deleted from.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="service">The service to delete the record from.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="accessToken">An access token for the specified service.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping an empty response. The HttpResult status code indicates the success or failure of the operation.</returns>
        public static async Task<HttpResult<EmptyResponse>> DeleteRecord(
            AtIdentifier repo,
            string collection,
            string rkey,
            AtCid? swapRecord,
            AtCid? swapCommit,
            Uri service,
            string accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            DeleteRecordRequest deleteRecordRequest = new(repo, collection, rkey) { SwapRecord = swapRecord, SwapCommit = swapCommit };

            AtProtoRequest<EmptyResponse> request = new ();

            return await request.Post(
                service,
                DeleteRecordEndpoint,
                deleteRecordRequest,
                accessToken,
                httpClientHandler,
                cancellationToken
                ).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets information about an account and repository, including the list of collections.
        /// </summary>
        /// <param name="atIdentifier">The <see cref="AtIdentifier"/> of the repo to retrieve information for.</param>
        /// <param name="service">The service to delete the record from.</param>
        /// <param name="accessToken">An access token for the specified service.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping a <see cref="RepoDescription"/>. The HttpResult status code indicates the success or failure of the operation.</returns>
        public static async Task<HttpResult<RepoDescription>> DescribeRepo(
            AtIdentifier atIdentifier,
            Uri service,
            string? accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            AtProtoRequest<RepoDescription> request = new();

            return await request.Get(
                service,
                $"{DescribeRepoEndpoint}?repo={atIdentifier}",
                accessToken,
                httpClientHandler,
                cancellationToken
                ).ConfigureAwait(false);
        }
    }
}
