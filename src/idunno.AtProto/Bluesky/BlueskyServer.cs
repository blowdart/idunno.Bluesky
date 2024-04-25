// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.AtProto.Bluesky
{
    /// <summary>
    /// Provides a class for sending requests to and receiving responses from an Bluesky service, identified by its service URI.
    /// </summary>
    internal partial class BlueskyServer
    {
        /// <summary>
        /// Creates a Bluesky post record on the specified <paramref name="service"/> and returns the <see cref="RecordResponse"/> for the request.
        /// </summary>
        /// <param name="post">The <see cref="Post"/> record to create.</param>
        /// <param name="creator">The <see cref="Did"/> of the record creator.</param>
        /// <param name="service">The service to create the record on.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="StrongReference"/> for the new post record.</returns>
        public static async Task<HttpResult<StrongReference>> CreatePost(
            Post post,
            Did creator,
            Uri service,
            string accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            return await AtProtoServer.CreateRecord(post, CollectionType.Post, creator, service, accessToken, httpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a like record against the record specified by the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The subject of the record to like.</param>
        /// <param name="creator">The DID of the actor creating the like.</param>
        /// <param name="service">The service to create the like record on.</param>
        /// <param name="accessToken">An access token for the specified service.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="StrongReference"/> for the new like record.</returns>
        public static async Task<HttpResult<StrongReference>> LikePost(
            StrongReference subject,
            Did creator,
            Uri service,
            string accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            PostLikeRecord like = new(creator);
            like.Values.Add("subject", subject);

            return await AtProtoServer.CreateRecord(like, CollectionType.Like, creator, service, accessToken, httpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a repost record against the record specified by the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The subject of the record to repost.</param>
        /// <param name="creator">The DID of the actor creating the repost.</param>
        /// <param name="service">The service to create the repost record on.</param>
        /// <param name="accessToken">An access token for the specified service.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public static async Task<HttpResult<StrongReference>> Repost(
            StrongReference subject,
            Did creator,
            Uri service,
            string accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            PostRepostRecord repost = new(creator);
            repost.Values.Add("subject", subject);

            return await AtProtoServer.CreateRecord(repost, CollectionType.Repost, creator, service, accessToken, httpClientHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}
