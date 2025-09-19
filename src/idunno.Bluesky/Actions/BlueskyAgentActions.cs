// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;

using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.RichText;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Record;
using idunno.Bluesky.Feed;

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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Follow(Handle handle, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            Did? didResolutionResult = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false);

            if (didResolutionResult is null)
            {
                Logger.FollowFailedAsHandleCouldNotResolve(_logger, handle);
            }

            if (didResolutionResult is null || cancellationToken.IsCancellationRequested)
            {
                return new AtProtoHttpResult<CreateRecordResult>(
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
        public async Task<AtProtoHttpResult<CreateRecordResult>> Follow(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            Follow follow = new(did);

            // We use the BlueskyTimestampedRecordValue class as the generic so the type discriminator appears in the serialized output.
            AtProtoHttpResult<CreateRecordResult> result = await CreateRecord<BlueskyTimestampedRecord>(
                record: follow,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: CollectionNsid.Follow,
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> Unfollow(Handle handle, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            ArgumentNullException.ThrowIfNull(handle);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> Unfollow(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<ProfileViewDetailed> userProfileResult = await GetProfile(did, cancellationToken: cancellationToken).ConfigureAwait(false);

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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="uri"/> does not point to a Bluesky follow record, or its RecordKey is null.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteFollow(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteFollow(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await DeleteFollow(strongReference.Uri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a block record in the authenticated user's repo for the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the actor to follow.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
        public async Task<AtProtoHttpResult<CreateRecordResult>> Block(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            Block block = new(did);

            // We use the BlueskyTimestampedRecordValue class as the generic so the type discriminator appears in the serialized output.
            return await CreateRecord<BlueskyTimestampedRecord>(
                record: block,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: CollectionNsid.Block,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unblocks the specified <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">The <see cref="Handle"/> of the actor to unblock.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> Unblock(Handle handle, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            ArgumentNullException.ThrowIfNull(handle);

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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> Unblock(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<ProfileViewDetailed> userProfileResult = await GetProfile(did, cancellationToken: cancellationToken).ConfigureAwait(false);

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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="uri"/> does not point to a Bluesky block record, or its RecordKey is null.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteBlock(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteBlock(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await DeleteBlock(strongReference.Uri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
        /// <param name="langs">The languages the post was written in.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is null.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="tags">Any optional tags to apply to the post.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
            string text,
            string langs,
            DateTimeOffset? createdAt = null,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            InteractionPreferences? interactionPreferences = null,
            PostSelfLabels? labels = null,
            ICollection<string>? tags = null,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            string[]? langsArray = null;

            if (langs is not null)
            {
                langsArray = [langs];
            }

            return await Post(
                text,
                createdAt,
                langsArray,
                threadGateRules,
                postGateRules,
                interactionPreferences,
                labels,
                tags,
                extractFacets,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
        /// <param name="langs">The languages the post was written in.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is null.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="tags">Any optional tags to apply to the post.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
            string text,
            DateTimeOffset? createdAt = null,
            ICollection<string>? langs = null,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            InteractionPreferences? interactionPreferences = null,
            PostSelfLabels? labels = null,
            ICollection<string>? tags = null,
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
                throw new AuthenticationRequiredException();
            }

            if (threadGateRules is null && interactionPreferences is not null)
            {
                threadGateRules = interactionPreferences.ThreadGateAllowRules;
            }

            if (postGateRules is null && interactionPreferences is not null)
            {
                postGateRules = interactionPreferences.PostGateEmbeddingRules;
            }

            return await Post(
                text,
                images: null,
                createdAt: createdAt,
                langs: langs,
                threadGateRules: threadGateRules,
                postGateRules: postGateRules,
                interactionPreferences: interactionPreferences,
                labels: labels,
                tags: tags,
                extractFacets: extractFacets,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record with an image.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="image">The image to attach to the post.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
        /// <param name="langs">The languages the post was written in.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is null.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="tags">Any optional tags to apply to the post.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
            string text,
            EmbeddedImage image,
            DateTimeOffset? createdAt = null,
            ICollection<string>? langs = null,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            InteractionPreferences? interactionPreferences = null,
            PostSelfLabels? labels = null,
            ICollection<string>? tags = null,
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
                throw new AuthenticationRequiredException();
            }

            List<EmbeddedImage>? images = null;

            if (image is not null)
            {
                images = [ image ];
            }

            return await Post(
                text,
                images: images,
                createdAt: createdAt,
                langs: langs,
                threadGateRules: threadGateRules,
                postGateRules: postGateRules,
                interactionPreferences: interactionPreferences,
                labels: labels,
                tags: tags,
                extractFacets: extractFacets,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record. with multiple images.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="images">Any images to attach to the post.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
        /// <param name="langs">The languages the post was written in.</param>
        /// <param name="threadGateRules">Any thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Any post gating rules to apply to the post, if any.</param>
        /// <param name="interactionPreferences">Any default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is null.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="tags">Any optional tags to apply to the post.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is null, empty or whitespace.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   if <paramref name="text"/> length is greater than the maximum number of characters or graphemes, or
        ///   <paramref name="images"/> contains more than the maximum allowed number of images.
        /// </exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
            string text,
            ICollection<EmbeddedImage>? images,
            DateTimeOffset? createdAt = null,
            ICollection<string>? langs = null,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            InteractionPreferences? interactionPreferences = null,
            PostSelfLabels? labels = null,
            ICollection<string>? tags = null,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            EmbeddedImages? embeddedImages = null;

            ArgumentException.ThrowIfNullOrWhiteSpace(text);

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
                throw new AuthenticationRequiredException();
            }

            DateTimeOffset creationDateTime = DateTimeOffset.UtcNow;
            if (createdAt is not null)
            {
                creationDateTime = createdAt.Value.ToUniversalTime();
            }

            Post post = new(
                text,
                createdAt: creationDateTime,
                langs : langs,
                embeddedRecord: embeddedImages,
                tags: tags);

            if (extractFacets)
            {
                IList<Facet> extractedFacets = await FacetExtractor.ExtractFacets(text, cancellationToken: cancellationToken).ConfigureAwait(false);
                post.Facets = extractedFacets;
            }

            if (labels is not null)
            {
                post.SetSelfLabels(labels);
            }

            return await CreatePost(
                post,
                threadGateRules: threadGateRules,
                postGateRules: postGateRules,
                interactionPreferences : interactionPreferences,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record with a video
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="video">The video to embed in the post.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
        /// <param name="langs">The languages the post was written in.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is null.</param>
        /// <param name="labels">Any optional self label settings for the post media content.</param>
        /// <param name="tags">Any optional tags to apply to the post.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is null</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
            string? text,
            EmbeddedVideo video,
            DateTimeOffset? createdAt = null,
            ICollection<string>? langs = null,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            InteractionPreferences? interactionPreferences = null,
            PostSelfLabels? labels = null,
            ICollection<string>? tags = null,
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
                throw new AuthenticationRequiredException();
            }

            DateTimeOffset creationDateTime = DateTimeOffset.UtcNow;
            if (createdAt is not null)
            {
                creationDateTime = createdAt.Value.ToUniversalTime();
            }

            Post post = new(
                text,
                createdAt: creationDateTime,
                langs: langs,
                embeddedRecord: video,
                tags: tags);

            if (extractFacets && text is not null)
            {
                IList<Facet> extractedFacets = await FacetExtractor.ExtractFacets(text, cancellationToken: cancellationToken).ConfigureAwait(false);
                post.Facets = extractedFacets;
            }

            if (labels is not null)
            {
                post.SetSelfLabels(labels);
            }

            return await CreatePost(
                post,
                threadGateRules: threadGateRules,
                postGateRules: postGateRules,
                interactionPreferences: interactionPreferences,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record containing just a external Open Graph embedded card.
        /// </summary>
        /// <remarks><para>Posts containing an embedded card do not require post text.</para></remarks>
        /// <param name="externalCard">An Open Graph embedded card.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
        /// <param name="langs">The languages the post was written in.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is null.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="tags">Any optional tags to apply to the post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="externalCard"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
            EmbeddedExternal externalCard,
            DateTimeOffset? createdAt = null,
            ICollection<string>? langs = null,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            InteractionPreferences? interactionPreferences = null,
            PostSelfLabels? labels = null,
            ICollection<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(externalCard);

            return await Post(
                text: string.Empty,
                externalCard: externalCard,
                createdAt: createdAt,
                langs: langs,
                threadGateRules: threadGateRules,
                postGateRules: postGateRules,
                interactionPreferences: interactionPreferences,
                extractFacets: false,
                labels: labels,
                tags: tags,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record containing text and an external Open Graph embedded card.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="externalCard">An Open Graph embedded card.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
        /// <param name="langs">The languages the post was written in.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is null.</param>
        /// <param name="labels">Optional self label settings for the post media content.</param>
        /// <param name="tags">Optional of tags to apply to the post.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="externalCard"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
            string text,
            EmbeddedExternal externalCard,
            DateTimeOffset? createdAt = null,
            ICollection<string>? langs = null,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            InteractionPreferences? interactionPreferences = null,
            PostSelfLabels? labels = null,
            bool extractFacets = true,
            ICollection<string>? tags = null,
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
                throw new AuthenticationRequiredException();
            }

            IList<Facet>? facets = null;

            if (extractFacets)
            {
                facets = await FacetExtractor.ExtractFacets(text, cancellationToken).ConfigureAwait(false);
            }

            var postBuilder = new PostBuilder(text, createdAt : createdAt, langs: langs, facets: facets, tags: tags);

            postBuilder.EmbedRecord(externalCard);

            if (interactionPreferences is not null)
            {
                postBuilder.ApplyInteractionPreferences(interactionPreferences);
            }

            if (threadGateRules is not null)
            {
                postBuilder.ThreadGateRules = [.. threadGateRules];
            }

            if (postGateRules is not null)
            {
                postBuilder.PostGateRules = [.. postGateRules];
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
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
            Post post,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            InteractionPreferences? interactionPreferences = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<CreateRecordResult> result = await CreatePost(
                post,
                threadGateRules: threadGateRules,
                postGateRules: postGateRules,
                interactionPreferences: interactionPreferences,
                cancellationToken: cancellationToken).ConfigureAwait(false);

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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Post(PostBuilder postBuilder, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(postBuilder);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            Post post;
            List<ThreadGateRule>? threadGateRules = null;
            List<PostGateRule>? postGateRules = null;

            lock (postBuilder)
            {
                post = postBuilder.ToPost();

                // The post builder already did the work in taking the default gating preferences and applying them.

                if (postBuilder.ThreadGateRules is not null)
                {
                    threadGateRules = [.. postBuilder.ThreadGateRules];
                }

                if (postBuilder.PostGateRules is not null)
                {
                    postGateRules = [.. postBuilder.PostGateRules];
                }
            }

            return await CreatePost(
                post,
                threadGateRules: threadGateRules,
                postGateRules: postGateRules,
                interactionPreferences: null, 
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the Bluesky post record specified by its <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the post to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="uri"/> does not point to a Bluesky feed post record, or its RecordKey is null.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeletePost(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
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
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeletePost(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await DeletePost(strongReference.Uri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, in reply to the <paramref name="parent"/> post.
        /// </summary>
        /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
        /// <param name="text">The text for the new reply.</param>
        /// <param name="tags">Any tags to apply to the reply.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from the post text automatically.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/> or <paramref name="text"/> is nul.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> ReplyTo(
            StrongReference parent,
            string text,
            ICollection<string>? tags = null,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.PostLengthInCharacters);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInCharacters);

            ArgumentNullException.ThrowIfNull(parent);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await InternalReplyTo(
                parent: parent,
                text:text,
                images:null,
                tags: tags,
                extractFacets: extractFacets,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, in reply to the <paramref name="parent"/> post.
        /// </summary>
        /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
        /// <param name="text">The text for the new reply</param>
        /// <param name="image">An image to attach to the reply.</param>
        /// <param name="tags">Any tags to apply to the reply.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from the post text automatically.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a null or empty tag.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/>, <paramref name="text"/> or <paramref name="image"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes,
        ///   or <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
        /// </exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> ReplyTo(
            StrongReference parent,
            string text,
            EmbeddedImage image,
            ICollection<string>? tags = null,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.PostLengthInCharacters);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInCharacters);

            ArgumentNullException.ThrowIfNull(parent);
            ArgumentNullException.ThrowIfNull(image);

            if (tags is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

                foreach (string tag in tags)
                {
                    ArgumentException.ThrowIfNullOrEmpty(tag);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
                }
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            List<EmbeddedImage> images = [ image ];

            return await InternalReplyTo(
                parent: parent,
                text: text,
                images: images,
                tags: tags,
                extractFacets: extractFacets,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a simple Bluesky post record with the specified <paramref name="text"/> in reply to the <paramref name="parent"/> post.
        /// </summary>
        /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
        /// <param name="text">The text for the new post</param>
        /// <param name="images">Any images to attach to the post.</param>
        /// <param name="tags">Any tags to apply to the reply.</param>
        /// <param name="extractFacets">Flag indicating whether facets should be extracted from the post text automatically.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown <paramref name="text"/> is null or empty, or <paramref name="tags"/> contains a null or empty tag.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes, or
        ///   <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
        /// </exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> ReplyTo(
            StrongReference parent,
            string text,
            ICollection<EmbeddedImage> images,
            ICollection<string>? tags = null,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.PostLengthInCharacters);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInCharacters);
            ArgumentNullException.ThrowIfNull(parent);
            ArgumentNullException.ThrowIfNull(images);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);

            if (tags is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

                foreach (string tag in tags)
                {
                    ArgumentException.ThrowIfNullOrEmpty(tag);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
                }
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await InternalReplyTo(
                parent: parent,
                text: text,
                images: images,
                tags: tags,
                extractFacets: extractFacets,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
 
        private async Task<AtProtoHttpResult<CreateRecordResult>> InternalReplyTo(
            StrongReference parent,
            string text,
            ICollection<EmbeddedImage>? images = null,
            ICollection<string>? tags = null,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.PostLengthInCharacters);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInCharacters);
            ArgumentNullException.ThrowIfNull(parent);

            if (images != null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);
            }

            if (tags is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

                foreach (string tag in tags)
                {
                    ArgumentException.ThrowIfNullOrEmpty(tag);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
                }
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<ReplyReferences> replyReferencesResult = await GetReplyReferences(parent, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!replyReferencesResult.Succeeded)
            {
                return new AtProtoHttpResult<CreateRecordResult>(
                    null,
                    replyReferencesResult.StatusCode,
                    replyReferencesResult.HttpResponseHeaders,
                    replyReferencesResult.AtErrorDetail,
                    replyReferencesResult.RateLimit);
            }

            PostBuilder postBuilder = new(text: text, langs: null, createdAt: null, labels: null, tags: tags)
            {
                InReplyTo = replyReferencesResult.Result,
            };

            if (images is not null)
            {
                postBuilder.Add(images);
            }

            if (extractFacets)
            {
                await postBuilder.ExtractFacets(
                    facetExtractor: FacetExtractor,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return await Post(postBuilder, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a repost record in the current user's repo for the post referred to by the specified <paramref name="post"/>.
        /// </summary>
        /// <param name="post">A <see cref="StrongReference"/> to the post to be reposted.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <remarks>
        /// <para>You should prefer to use <see cref="Repost(FeedViewPost, CancellationToken)"/> as this will ensure reposts of reposts create the right notifications.</para>
        /// </remarks>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Repost(StrongReference post, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (post.Uri.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException("post uri does not point to an {RecordCollections.Post} record.", nameof(post));
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            Repost repostRecord = new(post);
            return await Repost(repostRecord, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a repost record in the current user's repo for the post referred to by the specified <paramref name="uri"/> and <paramref name="cid"/>.
        /// </summary>
        /// <param name="uri">An <see cref="AtUri"/> to the record to be reposted.</param>
        /// <param name="cid">The <see cref="idunno.AtProto.Cid"/> of the record to be reposted.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, the uri collection, or <paramref name="cid"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="uri"/> does not point to a post.</exception>
        /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
        /// <remarks>
        /// <para>You should prefer to use <see cref="Repost(FeedViewPost, CancellationToken)"/> as this will ensure reposts of reposts create the right notifications.</para>
        /// </remarks>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Repost(AtUri uri, Cid cid, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(cid);

            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.Post);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await Repost(new StrongReference(uri, cid), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a repost record in the current user's repo for the specified in the <paramref name="postView"/>.
        /// </summary>
        /// <param name="postView">A <see cref="PostView"/> of the post to be liked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postView"/>, the PostView Uri, or the PostView Uri Collection is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the postView Uri does not point to a post.</exception>
        /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Repost(PostView postView, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(postView);

            ArgumentNullException.ThrowIfNull(postView.Uri);
            ArgumentNullException.ThrowIfNull(postView.Uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(postView.Uri.Collection, CollectionNsid.Post);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await Repost(postView.StrongReference, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a repost record in the current user's repo for the specified <see cref="FeedViewPost">post</see>.
        /// </summary>
        /// <param name="post">A <see cref="FeedViewPost"/> of the post to be reposted.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/>, or its Post is null, or the collection for the Post property's URI is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when the Post property on <paramref name="post"/> does not point to a post, or.
        ///   if a reason is present and the reason is typeof <see cref="ReasonRepost"/> and reason <see cref="StrongReference"/> is null, or the strong reference's
        ///   Uri property is null, or the strong reference's URI does not point to a Repost record.
        /// </exception>
        /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Repost(FeedViewPost post, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);
            ArgumentNullException.ThrowIfNull(post.Post);
            ArgumentNullException.ThrowIfNull(post.Post.Uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(post.Post.Uri.Collection, CollectionNsid.Post);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            Repost repostRecord;

            if (post.Reason is ReasonRepost postReason)
            {
                ArgumentNullException.ThrowIfNull(postReason.StrongReference);
                ArgumentNullException.ThrowIfNull(postReason.StrongReference.Uri);
                ArgumentNullException.ThrowIfNull(postReason.StrongReference.Uri.Collection);
                ArgumentOutOfRangeException.ThrowIfNotEqual(postReason.StrongReference.Uri.Collection, CollectionNsid.Repost);

                repostRecord = new(post.Post.StrongReference, postReason.StrongReference);
            }
            else
            {
                repostRecord = new(post.Post.StrongReference);
            }

            return await Repost(repostRecord, cancellationToken).ConfigureAwait(false);
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
        private async Task<AtProtoHttpResult<CreateRecordResult>> Repost(Repost repostRecord, CancellationToken cancellationToken = default)
        {
            // We use the BlueskyTimestampedRecordValue class as the generic so the type discriminator appears in the serialized output.
            return await CreateRecord<BlueskyTimestampedRecord>(
                record: repostRecord,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: CollectionNsid.Repost,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the repost record for the post referenced by <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the post to delete the repost of.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="uri"/> does not point to a Bluesky feed repost record, or its RecordKey is null.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteRepost(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.Post);
            ArgumentNullException.ThrowIfNull(uri.RecordKey);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            // Get the post view for the specified post so we can get the repost record uri if one exists.
            AtProtoHttpResult<Feed.PostView> postViewResult = await GetPostView(uri, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (postViewResult.StatusCode != HttpStatusCode.OK)
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    statusCode: postViewResult.StatusCode,
                    httpResponseHeaders: postViewResult.HttpResponseHeaders,
                    atErrorDetail: postViewResult.AtErrorDetail,
                    rateLimit: postViewResult.RateLimit);
            }

            if (postViewResult.Result is null)
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    statusCode: HttpStatusCode.BadRequest,
                    httpResponseHeaders: postViewResult.HttpResponseHeaders,
                    atErrorDetail: new AtErrorDetail("RecordNotFound", "Could not locate record:{uri}"),
                    rateLimit: postViewResult.RateLimit);
            }
            else if (postViewResult.Result.Viewer is null ||
                postViewResult.Result.Viewer.Repost is null)
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    statusCode: HttpStatusCode.NotFound,
                    httpResponseHeaders: postViewResult.HttpResponseHeaders,
                    atErrorDetail: new AtErrorDetail("RepostNotFound", "No repost record for the was found in {uri}."),
                    rateLimit: postViewResult.RateLimit);
            }

            return await DeleteRecord(
                collection: CollectionNsid.Repost,
                rKey: postViewResult.Result.Viewer.Repost.RecordKey!,
                cancellationToken:cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the repost record post referenced by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> of the post to delete the repost of.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteRepost(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await DeleteRepost(strongReference.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a like record in the current user's repo for the record pointed to by the <paramref name="strongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the record to be liked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> or the strongReference uri collection is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="strongReference"/> does not point to a post.</exception>
        /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
        /// <remarks>
        /// <para>You should prefer to use <see cref="Repost(FeedViewPost, CancellationToken)"/> as this will ensure reposts of reposts create the right notifications.</para>
        /// </remarks>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Like(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            ArgumentNullException.ThrowIfNull(strongReference.Uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(strongReference.Uri.Collection, CollectionNsid.Post);

            Record.Like likeRecord = new(strongReference);

            // We use the BlueskyTimestampedRecordValue class as the generic so the type discriminator appears in the serialized output.
            return await Like(likeRecord, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a like record in the current user's repo for the post pointed to by the <paramref name="uri"/> and <paramref name="cid"/>.
        /// </summary>
        /// <param name="uri">An <see cref="AtUri"/> to the record to be liked.</param>
        /// <param name="cid">The <see cref="idunno.AtProto.Cid"/> of the record to be liked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, the uri collection, or <paramref name="cid"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="uri"/> does not point to a post.</exception>
        /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
        /// <remarks>
        /// <para>You should prefer to use <see cref="Repost(FeedViewPost, CancellationToken)"/> as this will ensure reposts of reposts create the right notifications.</para>
        /// </remarks>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Like(AtUri uri, Cid cid, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(cid);

            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.Post);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await Like(new StrongReference(uri, cid), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a like record in the current user's repo for the specified in the <paramref name="postView"/>.
        /// </summary>
        /// <param name="postView">A <see cref="PostView"/> of the post to be liked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postView"/>, the PostView Uri, or the PostView Uri Collection is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the postView Uri does not point to a post.</exception>
        /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Like(PostView postView, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(postView);

            ArgumentNullException.ThrowIfNull(postView.Uri);
            ArgumentNullException.ThrowIfNull(postView.Uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(postView.Uri.Collection, CollectionNsid.Post);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await Like(postView.StrongReference, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a like record in the current user's repo for the specified <see cref="FeedViewPost">post</see>.
        /// </summary>
        /// <param name="post">A <see cref="FeedViewPost"/> of the post to be liked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/>, or its Post is null, or the collection for the Post property's URI is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when the Post property on <paramref name="post"/> does not point to a post, or.
        ///   if a reason is present and the reason is typeof <see cref="ReasonRepost"/> and reason <see cref="StrongReference"/> is null, or the strong reference's
        ///   Uri property is null, or the strong reference's URI does not point to a Repost record.
        /// </exception>
        /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Like(FeedViewPost post, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);
            ArgumentNullException.ThrowIfNull(post.Post);
            ArgumentNullException.ThrowIfNull(post.Post.Uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(post.Post.Uri.Collection, CollectionNsid.Post);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            Record.Like likeRecord;

            if (post.Reason is ReasonRepost postReason)
            {
                ArgumentNullException.ThrowIfNull(postReason.StrongReference);
                ArgumentNullException.ThrowIfNull(postReason.StrongReference.Uri);
                ArgumentNullException.ThrowIfNull(postReason.StrongReference.Uri.Collection);
                ArgumentOutOfRangeException.ThrowIfNotEqual(postReason.StrongReference.Uri.Collection, CollectionNsid.Repost);

                likeRecord = new(post.Post.StrongReference, postReason.StrongReference);
            }
            else
            {
                likeRecord = new(post.Post.StrongReference);
            }

            return await Like(likeRecord, cancellationToken).ConfigureAwait(false);
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
        private async Task<AtProtoHttpResult<CreateRecordResult>> Like(Record.Like likeRecord, CancellationToken cancellationToken = default)
        {
            // We use the BlueskyTimestampedRecordValue class as the generic so the type discriminator appears in the serialized output.
            return await CreateRecord<BlueskyTimestampedRecord>(
                record: likeRecord,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: CollectionNsid.Like,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the like record for the post referred to by <paramref name="uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the like record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="uri"/> does not point to a Bluesky feed repost record, or its RecordKey is null.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteLike(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.Post);
            ArgumentNullException.ThrowIfNull(uri.RecordKey);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            // Get the post view for the specified post so we can get the like record uri if one exists.
            AtProtoHttpResult<Feed.PostView> postViewResult = await GetPostView(uri, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (postViewResult.StatusCode != HttpStatusCode.OK)
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    statusCode: postViewResult.StatusCode,
                    httpResponseHeaders: postViewResult.HttpResponseHeaders,
                    atErrorDetail: postViewResult.AtErrorDetail,
                    rateLimit: postViewResult.RateLimit);
            }

            if (postViewResult.Result is null)
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    statusCode: HttpStatusCode.BadRequest,
                    httpResponseHeaders: postViewResult.HttpResponseHeaders,
                    atErrorDetail: new AtErrorDetail("RecordNotFound", "Could not locate record:{uri}"),
                    rateLimit: postViewResult.RateLimit);
            }
            else if (postViewResult.Result.Viewer is null ||
                postViewResult.Result.Viewer.Like is null)
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    statusCode: HttpStatusCode.NotFound,
                    httpResponseHeaders: postViewResult.HttpResponseHeaders,
                    atErrorDetail: new AtErrorDetail("LikeNotFound", "No like record was found in {uri}."),
                    rateLimit: postViewResult.RateLimit);
            }

            return await DeleteRecord(
                collection: CollectionNsid.Like,
                rKey: postViewResult.Result.Viewer.Like.RecordKey!,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes like record for the post specified by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> of the post whose like should be deleted.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteLike(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await DeleteLike(strongReference.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a post record, with the supplied <paramref name="text"/>, quoting the post identified by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
        /// <param name="text">The text for the new post.</param>
        /// <param name="tags">Any tags to apply to the quote post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a null or empty tag.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when the text length is longer than the maximum permitted or
        ///   <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
        /// </exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
            StrongReference strongReference,
            string text,
            ICollection<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (tags is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

                foreach (string tag in tags)
                {
                    ArgumentException.ThrowIfNullOrEmpty(tag);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
                }
            }

            return await Quote(
                strongReference: strongReference,
                text: text,
                images: null,
                tags: tags,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, if any, and <paramref name="image" />, quoting the post identified by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
        /// <param name="text">The text for the post</param>
        /// <param name="image">The image to attach to the post.</param>
        /// <param name="tags">Any tags to apply to the quote post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is null or <paramref name="tags"/> contains a null or empty tag.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> or <paramref name="image"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes or
        ///   <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
        /// </exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
            StrongReference strongReference,
            string text,
            EmbeddedImage image,
            ICollection<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);
            ArgumentException.ThrowIfNullOrEmpty(text);
            ArgumentNullException.ThrowIfNull(image);

            if (tags is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

                foreach (string tag in tags)
                {
                    ArgumentException.ThrowIfNullOrEmpty(tag);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
                }
            }

            return await Quote(
                strongReference: strongReference,
                text: text,
                images: [image],
                tags: tags,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, if any, and <paramref name="images" />, quoting the post identified by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
        /// <param name="text">The text for the new post</param>
        /// <param name="images">Any images to attach to the post.</param>
        /// <param name="tags">Any tags to apply to the quote post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a null or empty tag.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null or <paramref name="text"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes or
        ///   <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
        /// </exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
            StrongReference strongReference,
            string text,
            ICollection<EmbeddedImage>? images,
            ICollection<string>? tags = null,
            CancellationToken cancellationToken = default)
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

            if (tags is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

                foreach (string tag in tags)
                {
                    ArgumentException.ThrowIfNullOrEmpty(tag);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
                }
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            PostBuilder postBuilder = new(text, lang: Thread.CurrentThread.CurrentUICulture.Name, tags: tags)
            {
                QuotePost = strongReference,
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
        /// <param name="tags">Any tags to apply to the quote post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a null or empty tag.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
        /// </exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
            StrongReference strongReference,
            EmbeddedImage image,
            ICollection<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (tags is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

                foreach (string tag in tags)
                {
                    ArgumentException.ThrowIfNullOrEmpty(tag);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
                }
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await Quote(
                strongReference: strongReference,
                images: [image],
                tags: tags,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an Bluesky post record quoting the post identified by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
        /// <param name="images">Any images to attach to the quote post.</param>
        /// <param name="tags">Any tags to apply to the quote post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a null or empty tag.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="images"/> has too many images, or <paramref name="tags"/> has too many tags, or a tag that exceeds the maximum length.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to ApplyWrites().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to ApplyWrites().")]
        public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
            StrongReference strongReference,
            ICollection<EmbeddedImage>? images = null,
            ICollection<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            if (images is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);
            }

            if (tags is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

                foreach (string tag in tags)
                {
                    ArgumentException.ThrowIfNullOrEmpty(tag);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
                }
            }

            // This is a special case as there is no post text, it cannot go through the normal post APIs, it must go through the repo.ApplyWrites() api.
            Post postRecord = new()
            {
                EmbeddedRecord = new EmbeddedRecord(strongReference),
                Text = string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                Tags = tags
            };

            if (images is not null)
            {
                postRecord.EmbeddedRecord =
                    new EmbeddedRecordWithMedia(new EmbeddedRecord(strongReference), new EmbeddedImages(images));
            }

            CreateOperation createOperation = new(CollectionNsid.Post, TimestampIdentifier.Next(), postRecord);

            AtProtoHttpResult<ApplyWritesResults> result = await ApplyWrites(
                operations: [createOperation],
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                repo: Did,
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

                return new AtProtoHttpResult<CreateRecordResult>(
                    new CreateRecordResult(
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
                return new AtProtoHttpResult<CreateRecordResult>(
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteQuote(AtUri uri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await DeletePost(uri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the Bluesky quote record specified by its <paramref name="strongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> of the quote to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        /// <remarks>
        /// <para>A quote record is really a post record, so DeletePost() would also work. This method is just here for ease of discover and consistency.</para>
        /// </remarks>
        public async Task<AtProtoHttpResult<Commit>> DeleteQuote(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
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
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="imageAsBytes"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="mimeType"/> is null or empty.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
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
                throw new AuthenticationRequiredException();
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
        /// <exception cref="ArgumentException">Thrown when <paramref name="did"/> is null.</exception>
        public async Task<Handle?> LookupDid(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(did);

            AtProtoHttpResult<ProfileViewDetailed> result = await GetProfile(did, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                return result.Result.Handle;
            }
            else
            {
                return null;
            }
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
        private async Task<AtProtoHttpResult<CreateRecordResult>> CreatePost(
            Post post,
            ICollection<ThreadGateRule>? threadGateRules,
            ICollection<PostGateRule>? postGateRules,
            InteractionPreferences? interactionPreferences,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            if ((threadGateRules is null && postGateRules is null && interactionPreferences is null) && !string.IsNullOrEmpty(post.Text))
            {
                // We use the BlueskyTimestampedRecordValue class as the generic so the type discriminator appears in the serialized output.
                return await CreateRecord<BlueskyTimestampedRecord>(
                    record: post,
                    jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                    collection: CollectionNsid.Post,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // If a post has no text (which is possible if there are embedded records
                // or it has gates and creating the post and gates need to be atomic
                // we have to use ApplyWrites() rather than CreateRecord()
                List<WriteOperation> writeRequests = [];

                // We need to generate a record key to hang it all together.
                RecordKey rKey = TimestampIdentifier.Next();
                AtUri postUri = new($"at://{Did}/{CollectionNsid.Post}/{rKey}");

                writeRequests.Add(new CreateOperation(CollectionNsid.Post, rKey, post));

                if (threadGateRules is null && interactionPreferences is not null)
                {
                    threadGateRules = interactionPreferences.ThreadGateAllowRules;
                }

                if (postGateRules is null && interactionPreferences is not null)
                {
                    postGateRules = interactionPreferences.PostGateEmbeddingRules;
                }

                if (threadGateRules is not null)
                {
                    writeRequests.Add(new CreateOperation(
                        CollectionNsid.ThreadGate,
                        rKey,
                        new ThreadGate(postUri, threadGateRules)));
                }

                if (postGateRules is not null)
                {
                    writeRequests.Add(new CreateOperation(
                        CollectionNsid.PostGate,
                        rKey,
                        new PostGate(postUri, postGateRules)));
                }

                AtProtoHttpResult<ApplyWritesResults> response =
                    await ApplyWrites(
                        writeRequests,
                        jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                        repo: Did,
                        validate: true,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                if (response.Succeeded)
                {
                    Logger.CreatePostWithGatesSucceeded(_logger, rKey, Did);

                    CreateRecordResult? createRecordResult = null;

                    foreach (IApplyWritesResult result in response.Result.Results)
                    {
                        if (result is ApplyWritesCreateResult createResult && createResult.Uri == postUri)
                        {
                            createRecordResult = new CreateRecordResult(postUri, createResult.Cid, validationStatus: createResult.ValidationStatus, commit: response.Result.Commit);
                            break;
                        }
                    }

                    return new AtProtoHttpResult<CreateRecordResult>(createRecordResult, response.StatusCode, response.HttpResponseHeaders, response.AtErrorDetail, response.RateLimit);
                }
                else
                {
                    Logger.CreatePostWithGatesFailed(_logger, response.StatusCode, Did, response.AtErrorDetail?.Error, response.AtErrorDetail?.Message);
                    return new AtProtoHttpResult<CreateRecordResult>(null, response.StatusCode, response.HttpResponseHeaders, response.AtErrorDetail, response.RateLimit);
                }
            }
        }
    }
}
