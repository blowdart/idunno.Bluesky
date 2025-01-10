// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;

using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.RichText;
using idunno.Bluesky.Actions;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Creates a follow record in the authenticated user's repo for the specified <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">The <see cref="Handle"/> of the actor to follow.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="handle"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Follow(Handle handle, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            Did? didResolutionResult = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false);

            if (didResolutionResult is null)
            {
                Logger.FollowFailedAsHandleCouldNotResolve(_logger, handle);
            }

            if (didResolutionResult is null || cancellationToken.IsCancellationRequested)
            {
                return new AtProtoHttpResult<CreateRecordResponse>(
                    null,
                    HttpStatusCode.NotFound,
                    null,
                    null);
            }

            return await Follow(didResolutionResult, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a follow record in the authenticated user's repo for the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the actor to follow.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="did"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Follow(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            FollowRecordValue follow = new(did);

            AtProtoHttpResult<CreateRecordResponse> result = await CreateRecord(
                follow,
                CollectionNsid.Follow,
                Did,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                Logger.FollowSucceeded(_logger, Did, follow.Subject);
            }
            else
            {
                Logger.FollowFailedAtApiLayer(_logger, Did, follow.Subject);
            }

            return result;
        }

        /// <summary>
        /// Unfollows the specified <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">The <see cref="Handle"/> of the actor to unfollow.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="handle"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> Unfollow(Handle handle, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            ArgumentNullException.ThrowIfNull(handle);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            Did? didResolutionResult = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false);

            if (didResolutionResult is null)
            {
                Logger.UnfollowFailedAsHandleCouldNotResolve(_logger, handle);
            }

            if (didResolutionResult is null || cancellationToken.IsCancellationRequested)
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    HttpStatusCode.NotFound,
                    null,
                    null);
            }

            return await Unfollow(didResolutionResult, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unfollows the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the actor to unfollow.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="did"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> Unfollow(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            AtProtoHttpResult<Actor.ProfileViewDetailed> userProfileResult = await GetProfile(did, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!userProfileResult.Succeeded)
            {
                Logger.UnfollowFailedAsHandleCouldNotGetUserProfile(_logger, did);

                return new AtProtoHttpResult<Commit>(
                    null,
                    userProfileResult.StatusCode,
                    userProfileResult.HttpResponseHeaders,
                    userProfileResult.AtErrorDetail,
                    userProfileResult.RateLimit);
            }

            // Now check to see if the current user has a follow relationship with the did

            if (userProfileResult.Result.Viewer is null || userProfileResult.Result.Viewer.Following is null)
            {
                Logger.UnfollowFailedAsHandleCouldNotGetUserIsNotFollowing(_logger, did);

                return new AtProtoHttpResult<Commit>(
                    null,
                    HttpStatusCode.NotFound,
                    userProfileResult.HttpResponseHeaders,
                    null,
                    userProfileResult.RateLimit);
            }

            return await DeleteFollow(userProfileResult.Result.Viewer.Following, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the Bluesky follow record specified by its <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the follow record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="uri"/> does not point to a Bluesky follow record, or its RecordKey is null.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteFollow(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (uri.Collection != CollectionNsid.Follow)
            {
                throw new ArgumentException($"uri does not point to an {CollectionNsid.Follow} record", nameof(uri));
            }

            if (uri.RecordKey is null)
            {
                throw new ArgumentException("uri RecordKey is null", nameof(uri));
            }

            return await DeleteRecord(uri.Collection, uri.RecordKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the Bluesky follow record specified by its <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> of the follow record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteFollow(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeleteFollow(strongReference.Uri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a block record in the authenticated user's repo for the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the actor to follow.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="did"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Block(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            BlockRecordValue block = new(did);

            return await CreateRecord(
                block,
                CollectionNsid.Block,
                Did,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unblocks the specified <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">The <see cref="Handle"/> of the actor to unblock.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="handle"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> Unblock(Handle handle, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            ArgumentNullException.ThrowIfNull(handle);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            Did? didResolutionResult = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false);

            if (didResolutionResult is null)
            {
                Logger.BlockFailedAsHandleCouldNotResolve(_logger, handle);
            }

            if (didResolutionResult is null || cancellationToken.IsCancellationRequested)
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    HttpStatusCode.NotFound,
                    null,
                    null);
            }

            return await Unblock(didResolutionResult, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unblocks the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the actor to unfollow.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="did"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> Unblock(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            AtProtoHttpResult<Actor.ProfileViewDetailed> userProfileResult = await GetProfile(did, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!userProfileResult.Succeeded)
            {
                Logger.UnblockFailedAsHandleCouldNotGetUserProfile(_logger, did);

                return new AtProtoHttpResult<Commit>(
                    null,
                    userProfileResult.StatusCode,
                    userProfileResult.HttpResponseHeaders,
                    userProfileResult.AtErrorDetail,
                    userProfileResult.RateLimit);
            }

            // Now check to see if the current user has a follow relationship with the did

            if (userProfileResult.Result.Viewer is null || userProfileResult.Result.Viewer.Blocking is null)
            {
                Logger.UnblockFailedAsHandleCouldNotGetUserIsNotFollowing(_logger, did);

                return new AtProtoHttpResult<Commit>(
                    null,
                    HttpStatusCode.NotFound,
                    userProfileResult.HttpResponseHeaders,
                    null,
                    userProfileResult.RateLimit);
            }

            return await DeleteBlock(userProfileResult.Result.Viewer.Blocking, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the Bluesky block record specified by its <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the block record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="uri"/> does not point to a Bluesky block record, or its RecordKey is null.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteBlock(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (uri.Collection != CollectionNsid.Block)
            {
                throw new ArgumentException($"uri does not point to an {CollectionNsid.Block} record", nameof(uri));
            }

            if (uri.RecordKey is null)
            {
                throw new ArgumentException("uri RecordKey is null", nameof(uri));
            }

            return await DeleteRecord(uri.Collection, uri.RecordKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the Bluesky block record specified by its <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> of the block record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteBlock(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeleteBlock(strongReference.Uri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">if <paramref name="text"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Post(
            string text,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            PostSelfLabels? labels = null,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(text),
                    $"text cannot have a be longer than than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await Post(
                text,
                images: null,
                threadGateRules: threadGateRules,
                postGateRules: postGateRules,
                labels: labels,
                extractFacets: extractFacets,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="image">The image to attach to the post.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">if <paramref name="text"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Post(
            string text,
            EmbeddedImage image,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            PostSelfLabels? labels = null,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(text),
                    $"text cannot have a be longer than than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            List<EmbeddedImage>? images = null;

            if (image is not null)
            {
                images = [ image ];
            }

            return await Post(
                text,
                images: images,
                threadGateRules: threadGateRules,
                postGateRules: postGateRules,
                labels: labels,
                extractFacets: extractFacets,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="images">Any images to attach to the post.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="text"/> is null, empty or whitespace.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   if <paramref name="text"/> length is greater than the maximum number of characters or graphemes, or
        ///   <paramref name="images"/> contains more than the maximum allowed number of images.
        /// </exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Post(
            string text,
            ICollection<EmbeddedImage>? images,
            ICollection<ThreadGateRule>? threadGateRules,
            ICollection<PostGateRule>? postGateRules,
            PostSelfLabels? labels = null,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            EmbeddedImages? embeddedImages = null;

            ArgumentNullException.ThrowIfNullOrWhiteSpace(text);

            if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(text),
                    $"text cannot have a be longer than than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
            }

            if (images != null)
            {
                if (images.Count > Maximum.ImagesInPost)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(images),
                        $"Cannot have more than {Maximum.ImagesInPost} images.");
                }
                else
                {
                    embeddedImages = new EmbeddedImages(images);
                }
            }

            if (threadGateRules != null && threadGateRules.Count > Maximum.ThreadGateRules)
            {
                throw new ArgumentOutOfRangeException(nameof(threadGateRules), $"Cannot have more than {Maximum.ThreadGateRules} rules.");
            }

            if (postGateRules != null && postGateRules.Count > Maximum.PostGateRules)
            {
                throw new ArgumentOutOfRangeException(nameof(postGateRules), $"Cannot have more than {Maximum.PostGateRules} rules.");
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            Post post = new(
                text,
                langs : [Thread.CurrentThread.CurrentUICulture.Name],
                embeddedRecord: embeddedImages);

            if (extractFacets)
            {
                IList<Facet> extractedFacets = await _facetExtractor.ExtractFacets(text, cancellationToken: cancellationToken).ConfigureAwait(false);
                post.Facets = extractedFacets;
            }

            if (labels is not null)
            {
                post.SetSelfLabels(labels);
            }

            return await CreatePost(post, Did, threadGateRules, postGateRules, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="video">The video to embed in the post.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is null</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Post(
            string? text,
            EmbeddedVideo video,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            PostSelfLabels? labels = null,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(video);

            if (text is not null &&
                (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes))
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.PostLengthInCharacters);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInGraphemes);
            }

            if (threadGateRules is not null && threadGateRules.Count > Maximum.ThreadGateRules)
            {
                throw new ArgumentOutOfRangeException(nameof(threadGateRules), $"Cannot have more than {Maximum.ThreadGateRules} rules.");
            }

            if (postGateRules is not null && postGateRules.Count > Maximum.PostGateRules)
            {
                throw new ArgumentOutOfRangeException(nameof(postGateRules), $"Cannot have more than {Maximum.PostGateRules} rules.");
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            Post post = new(
                text,
                langs: [Thread.CurrentThread.CurrentUICulture.Name],
                embeddedRecord: video);

            if (extractFacets && text is not null)
            {
                IList<Facet> extractedFacets = await _facetExtractor.ExtractFacets(text, cancellationToken: cancellationToken).ConfigureAwait(false);
                post.Facets = extractedFacets;
            }

            if (labels is not null)
            {
                post.SetSelfLabels(labels);
            }

            return await CreatePost(post, Did, threadGateRules, postGateRules, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record containing just a external Open Graph embedded card.
        /// </summary>
        /// <remarks><para>Posts containing an embedded card do not require post text.</para></remarks>
        /// <param name="externalCard">An Open Graph embedded card.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="externalCard"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Post(
            EmbeddedExternal externalCard,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            PostSelfLabels? labels = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(externalCard);

            return await Post(
                text: string.Empty,
                externalCard: externalCard,
                threadGateRules: threadGateRules,
                postGateRules: postGateRules,
                extractFacets: false,
                labels: labels,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// Creates a Bluesky post record containing a external Open Graph embedded card.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="externalCard">An Open Graph embedded card.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="externalCard"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Post(
            string text,
            EmbeddedExternal externalCard,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            PostSelfLabels? labels = null,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(externalCard);

            if ((text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(text),
                    $"text cannot have a be longer than than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            IList<Facet>? facets = null;

            if (extractFacets)
            {
                facets = await _facetExtractor.ExtractFacets(text, cancellationToken).ConfigureAwait(false);
            }

            var postBuilder = new PostBuilder(text, facets: facets);
            postBuilder.EmbedRecord(externalCard);

            if (threadGateRules is not null)
            {
                postBuilder.ThreadGateRules = new List<ThreadGateRule>(threadGateRules);
            }

            if (postGateRules is not null)
            {
                postBuilder.PostGateRules = new List<PostGateRule>(postGateRules);
            }

            if (labels is not null)
            {
                postBuilder.SetSelfLabels(labels);
            }

            return await Post(postBuilder, cancellationToken: cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// Creates a post record from the specified <paramref name="post"/>.
        /// </summary>
        /// <param name="post">The post to create the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Post(Post post, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            AtProtoHttpResult<CreateRecordResponse> result = await CreatePost(post, Did, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                Logger.CreatePostWithPostSucceeded(_logger, Did, result.Result.Uri, result.Result.Cid);
            }
            else
            {
                Logger.CreatePostWithPostFailed(_logger, result.StatusCode, Did, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message);
            }

            return result;
        }

        /// <summary>
        /// Creates a Bluesky post record from the specified <paramref name="postBuilder"/>.
        /// </summary>
        /// <param name="postBuilder">The <see cref="PostBuilder"/> to use to create the record.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="postBuilder"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Post(PostBuilder postBuilder, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            Post post;
            List<ThreadGateRule>? threadGateRules = null;
            List<PostGateRule>? postGateRules = null;

            lock (postBuilder)
            {
                post = postBuilder.ToPostRecord();

                if (postBuilder.ThreadGateRules is not null)
                {
                    threadGateRules = new List<ThreadGateRule>(postBuilder.ThreadGateRules);
                }

                if (postBuilder.PostGateRules is not null)
                {
                    postGateRules = new List<PostGateRule>(postBuilder.PostGateRules);
                }
            }

            return await CreatePost(
                post,
                Did,
                threadGateRules,
                postGateRules,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the Bluesky post record specified by its <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the post to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="uri"/> does not point to a Bluesky feed post record, or its RecordKey is null.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeletePost(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (uri.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException($"uri does not point to an {CollectionNsid.Post} record", nameof(uri));
            }

            if (uri.RecordKey is null)
            {
                throw new ArgumentException("uri RecordKey is null", nameof(uri));
            }

            return await DeleteRecord(uri.Collection, uri.RecordKey, cancellationToken:cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the Bluesky post record specified by its <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> of the post to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeletePost(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeletePost(strongReference.Uri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, in reply to the <paramref name="parent"/> post.
        /// </summary>
        /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
        /// <param name="text">The text for the new reply</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/> or <paramref name="text"/> is nul.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> ReplyTo(StrongReference parent, string text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(parent);

            if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(text),
                    $"text cannot have a be longer than than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await ReplyTo(parent, text, images:null, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, in reply to the <paramref name="parent"/> post.
        /// </summary>
        /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
        /// <param name="text">The text for the new reply</param>
        /// <param name="image">An image to attach to the reply.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="parent"/>, <paramref name="text"/> or <paramref name="image"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> ReplyTo(StrongReference parent, string text, EmbeddedImage image, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(parent);
            ArgumentNullException.ThrowIfNull(image);

            if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(text),
                    $"text cannot have a be longer than than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            List<EmbeddedImage> images = [ image ];

            return await ReplyTo(parent, text, images, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a simple Bluesky post record with the specified <paramref name="text"/> in reply to the <paramref name="parent"/> post.
        /// </summary>
        /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
        /// <param name="text">The text for the new post</param>
        /// <param name="images">Any images to attach to the post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="parent"/> is null or <paramref name="text"/> is null or empty.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> ReplyTo(StrongReference parent, string text, ICollection<EmbeddedImage>? images, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(text);
            ArgumentNullException.ThrowIfNull(parent);

            if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
            {
                throw new ArgumentOutOfRangeException(nameof(text), $"text cannot have be longer than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
            }

            if (images != null && images.Count > Maximum.ImagesInPost)
            { 
                throw new ArgumentOutOfRangeException( nameof(images), $"cannot have more than {Maximum.ImagesInPost} images.");
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            AtProtoHttpResult<ReplyReferences> replyReferencesResult = await GetReplyReferences(parent, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!replyReferencesResult.Succeeded)
            {
                return new AtProtoHttpResult<CreateRecordResponse>(
                    null,
                    replyReferencesResult.StatusCode,
                    replyReferencesResult.HttpResponseHeaders,
                    replyReferencesResult.AtErrorDetail,
                    replyReferencesResult.RateLimit);
            }

            PostBuilder postBuilder = new(text)
            {
                InReplyTo = replyReferencesResult.Result
            };

            if (images is not null)
            {
                postBuilder.Add(images);
            }

            return await Post(postBuilder, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a repost record for the specified <paramref name="post"/> in the current user's repo.
        /// </summary>
        /// <param name="post">A <see cref="StrongReference"/> to the post to be reposted.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Repost(StrongReference post, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (post.Uri.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException("post uri does not point to an {RecordCollections.Post} record.", nameof(post));
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            RepostRecordValue repostRecord = new(post);

            return await CreateRecord(
                repostRecord,
                CollectionNsid.Repost,
                Did,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the repost record specified by its <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the repost record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="uri"/> does not point to a Bluesky feed repost record, or its RecordKey is null.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteRepost(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (uri.Collection != CollectionNsid.Repost)
            {
                throw new ArgumentException($"uri does not point to an {CollectionNsid.Repost} record", nameof(uri));
            }

            if (uri.RecordKey is null)
            {
                throw new ArgumentException("uri RecordKey is null", nameof(uri));
            }

            return await DeleteRecord(uri.Collection, uri.RecordKey, cancellationToken:cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the repost record specified by its <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> of the repost record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteRepost(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeleteRepost(strongReference.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a like record in the current user's repo for the record pointed to by the <paramref name="strongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the record to be liked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Like(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            LikeRecordValue likeRecord = new(strongReference);

            return await CreateRecord(
                likeRecord,
                CollectionNsid.Like,
                Did,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the like record specified by its <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the like record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="uri"/> does not point to a Bluesky feed repost record, or its RecordKey is null.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteLike(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (uri.Collection != CollectionNsid.Like)
            {
                throw new ArgumentException($"uri does not point to an {CollectionNsid.Like} record", nameof(uri));
            }

            if (uri.RecordKey is null)
            {
                throw new ArgumentException("uri RecordKey is null", nameof(uri));
            }

            return await DeleteRecord(uri.Collection, uri.RecordKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes like record specified by its <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> of the like record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteLike(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeleteLike(strongReference.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a post record, with the supplied <paramref name="text"/>, quoting the post identified by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
        /// <param name="text">The text for the new post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is longer than the maximum permitted.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Quote(StrongReference strongReference, string text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            return await Quote(strongReference, text, images: null, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, if any, and <paramref name="image" />, quoting the post identified by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
        /// <param name="text">The text for the post</param>
        /// <param name="image">The image to attach to the post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> or <paramref name="image"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Quote(StrongReference strongReference, string text, EmbeddedImage image, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);
            ArgumentException.ThrowIfNullOrEmpty(text);
            ArgumentNullException.ThrowIfNull(image);

            return await Quote(strongReference, text, [image], cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, if any, and <paramref name="images" />, quoting the post identified by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
        /// <param name="text">The text for the new post</param>
        /// <param name="images">Any images to attach to the post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null or <paramref name="text"/> is null or empty.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Quote(StrongReference strongReference, string text, ICollection<EmbeddedImage>? images, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);
            ArgumentNullException.ThrowIfNull(text);

            if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
            {
                throw new ArgumentOutOfRangeException(nameof(text), $"text cannot have be longer than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
            }

            if (images is not null && images.Count > Maximum.ImagesInPost)
            {
                throw new ArgumentOutOfRangeException(nameof(images), $"cannot have more than {Maximum.ImagesInPost} images.");
            }

            if (images is not null && images.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(images), $"cannot be an empty collection.");
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            PostBuilder postBuilder = new()
            {
                QuotePost = strongReference,
                Text = text,
                Languages = [Thread.CurrentThread.CurrentUICulture.Name]
            };

            if (images is not null)
            {
                postBuilder.Add(images);
            }

            return await Post(postBuilder, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an Bluesky post record quoting the post identified by <see cref="StrongReference"/> with just an image.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
        /// <param name="image">The image to attach to the quote.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="image"/> is null</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Quote(
            StrongReference strongReference,
            EmbeddedImage image,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await Quote(strongReference, [image], cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an Bluesky post record  quoting the post identified by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
        /// <param name="images">Any images to attach to the post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="strongReference"/> is null</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Quote(
            StrongReference strongReference,
            ICollection<EmbeddedImage>? images = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (images?.Count > Maximum.ImagesInPost)
            {
                throw new ArgumentException($"Cannot have more than {Maximum.ImagesInPost} images", nameof(images));
            }

            // This is a special case as there is no post text, it cannot go through the normal post APIs, it must go through the repo.ApplyWrites() api.

            Post postRecord = new()
            {
                EmbeddedRecord = new EmbeddedRecord(strongReference),
                Text = string.Empty,
                CreatedAt = DateTimeOffset.UtcNow
            };

            if (images is not null)
            {
                postRecord.EmbeddedRecord =
                    new EmbeddedRecordWithMedia(new EmbeddedRecord(strongReference), new EmbeddedImages(images));
            }

            ApplyWritesCreate applyWritesCreate = new(CollectionNsid.Post, TimestampIdentifier.Generate(), postRecord);

            AtProtoHttpResult<ApplyWritesResponse> result = await ApplyWrites(
                [applyWritesCreate],
                Did,
                cid: null,
                validate: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                if (result.Result.Results.Count == 0 ||
                    result.Result.Results.Count > 1)
                {
                    Logger.QuoteCreateSucceededButResultResultsIsNotCountOne(_logger, result.Result.Results.Count);
                    throw new InvalidOperationException($"ApplyWrites() returned a results array with a count of {result.Result.Results.Count}");
                }

                if (result.Result.Results.First() is not ApplyWritesCreateResult recordResult)
                {
                    Logger.QuoteCreateSucceededButReturnResultUnexpectedType(_logger, result.Result.Results.First().GetType());
                    throw new InvalidOperationException($"ApplyWrites() result was not of type ApplyWritesCreateResult.");
                }

                return new AtProtoHttpResult<CreateRecordResponse>(
                    new CreateRecordResponse(
                        recordResult.Uri,
                        recordResult.Cid,
                        result.Result.Commit,
                        recordResult.ValidationStatus),
                    result.StatusCode,
                    result.HttpResponseHeaders,
                    result.AtErrorDetail,
                    result.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<CreateRecordResponse>(
                    null,
                    result.StatusCode,
                    result.HttpResponseHeaders,
                    result.AtErrorDetail,
                    result.RateLimit);
            }
        }

        /// <summary>
        /// Deletes the Bluesky quote record specified by its <paramref name="uri" />.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the quote to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteQuote(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeletePost(uri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the Bluesky quote record specified by its <paramref name="strongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> of the quote to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        /// <remarks>
        /// <para>A quote record is really a post record, so DeletePost() would also work. This method is just here for ease of discover and consistency.</para>
        /// </remarks>
        public async Task<AtProtoHttpResult<Commit>> DeleteQuote(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeletePost(strongReference, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Uploads an image to be referenced in a post.
        /// </summary>
        /// <param name="imageAsBytes">The image, as a byte array.</param>
        /// <param name="mimeType">The mime type of the image. No validation is performed on this value.</param>
        /// <param name="altText">The AltText (for accessibility) for the image.</param>
        /// <param name="aspectRatio">The image's aspect ratio.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageAsBytes"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when if <paramref name="imageAsBytes"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when if <paramref name="mimeType"/> is null or empty.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<EmbeddedImage>> UploadImage(
            byte[] imageAsBytes,
            string mimeType,
            string altText,
            AspectRatio? aspectRatio,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imageAsBytes);
            ArgumentOutOfRangeException.ThrowIfZero(imageAsBytes.Length);
            ArgumentException.ThrowIfNullOrEmpty(mimeType);


            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }


            AtProtoHttpResult<Blob> uploadResult = await UploadBlob(
                imageAsBytes,
                mimeType,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (uploadResult.Succeeded)
            {
                Logger.ImageUploadSucceed(_logger, Did, uploadResult.Result.Reference.Link);

                return new AtProtoHttpResult<EmbeddedImage>(
                    new EmbeddedImage(uploadResult.Result, altText, aspectRatio),
                    uploadResult.StatusCode,
                    uploadResult.HttpResponseHeaders,
                    uploadResult.AtErrorDetail,
                    uploadResult.RateLimit);
            }
            else
            {
                Logger.ImageUploadFailed(_logger, uploadResult.StatusCode, Did, uploadResult.AtErrorDetail?.Error, uploadResult.AtErrorDetail?.Message);

                return new AtProtoHttpResult<EmbeddedImage>(
                    null,
                    uploadResult.StatusCode,
                    uploadResult.HttpResponseHeaders,
                    uploadResult.AtErrorDetail,
                    uploadResult.RateLimit);
            }
        }

        /// <summary>
        /// Performs a reverse lookup on a DID and returns its handle.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> to lookup.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="did"/> is null.</exception>
        public async Task<Handle?> LookupDid(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(did);

            AtProtoHttpResult<Actor.ProfileViewDetailed> result = await GetProfile(did, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                return result.Result.Handle;
            }
            else
            {
                return null;
            }
        }

        private async Task<AtProtoHttpResult<CreateRecordResponse>> CreatePost(
            Post post,
            Did owner,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            CancellationToken cancellationToken = default)
        {
            if ((threadGateRules is null && postGateRules is null) && !string.IsNullOrEmpty(post.Text))
            {
                return await CreateRecord(
                    post,
                    CollectionNsid.Post,
                    owner,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // If a post has no text (which is possible if there are embedded records
                // or it has gates and creating the post and gates need to be atomic
                // we have to use ApplyWrites() rather than CreateRecord()
                List<ApplyWritesRequestValueBase> writeRequests = [];

                // We need to generate a record key to hang it all together.
                RecordKey rKey = TimestampIdentifier.Generate();
                AtUri postUri = new($"at://{owner}/{CollectionNsid.Post}/{rKey}");

                writeRequests.Add(new ApplyWritesCreate(CollectionNsid.Post, rKey, post));

                if (threadGateRules is not null)
                {
                    writeRequests.Add(new ApplyWritesCreate(
                        CollectionNsid.ThreadGate,
                        rKey,
                        new ThreadGate(postUri, threadGateRules)));
                }

                if (postGateRules is not null)
                {
                    writeRequests.Add(new ApplyWritesCreate(
                        CollectionNsid.PostGate,
                        rKey,
                        new PostGate(postUri, postGateRules)));
                }

                AtProtoHttpResult<ApplyWritesResponse> response =
                    await ApplyWrites(writeRequests, owner, validate: true, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (response.Succeeded)
                {
                    Logger.CreatePostWithGatesSucceeded(_logger, rKey, owner);

                    CreateRecordResponse? createRecordResponse = null;

                    foreach (ApplyWritesResultBase result in response.Result.Results)
                    {
                        if (result is ApplyWritesCreateResult createResult && createResult.Uri == postUri)
                        {
                            createRecordResponse = new CreateRecordResponse(postUri, createResult.Cid, validationStatus: createResult.ValidationStatus, commit: response.Result.Commit);
                            break;
                        }
                    }

                    return new AtProtoHttpResult<CreateRecordResponse>(createRecordResponse, response.StatusCode, response.HttpResponseHeaders, response.AtErrorDetail, response.RateLimit);
                }
                else
                {
                    Logger.CreatePostWithGatesFailed(_logger, response.StatusCode, owner, response.AtErrorDetail?.Error, response.AtErrorDetail?.Message);
                    return new AtProtoHttpResult<CreateRecordResponse>(null, response.StatusCode, response.HttpResponseHeaders, response.AtErrorDetail, response.RateLimit);
                }
            }
        }

    }
}
