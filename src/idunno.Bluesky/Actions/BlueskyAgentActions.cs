// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actions.Model;
using idunno.Bluesky.Embed;
using idunno.AtProto.Repo.Models;
using idunno.Bluesky.Feed.Gates;

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

            NewFollowRecord followRecord = new(did);

            return await CreateRecord(
                followRecord,
                CollectionNsid.Follow,
                Did,
                cancellationToken: cancellationToken).ConfigureAwait(false);
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
                return new AtProtoHttpResult<Commit>(
                    null,
                    userProfileResult.StatusCode,
                    userProfileResult.AtErrorDetail,
                    userProfileResult.RateLimit);
            }

            // Now check to see if the current user has a follow relationship with the did

            if (userProfileResult.Result.Viewer is null || userProfileResult.Result.Viewer.Following is null)
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    HttpStatusCode.NotFound,
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

            NewFollowRecord followRecord = new(did);

            return await CreateRecord(
                followRecord,
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
                return new AtProtoHttpResult<Commit>(
                    null,
                    userProfileResult.StatusCode,
                    userProfileResult.AtErrorDetail,
                    userProfileResult.RateLimit);
            }

            // Now check to see if the current user has a follow relationship with the did

            if (userProfileResult.Result.Viewer is null || userProfileResult.Result.Viewer.Blocking is null)
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    HttpStatusCode.NotFound,
                    null,
                    userProfileResult.RateLimit);
            }

            return await DeleteFollow(userProfileResult.Result.Viewer.Blocking, cancellationToken: cancellationToken).ConfigureAwait(false);
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
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="text"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Post(
            string text,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(text);

            if (text.Length > Maximum.PostLengthInCharacters || text.GetLengthInGraphemes() > Maximum.PostLengthInGraphemes)
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
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="image">The image to attach to the post.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="text"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Post(
            string text,
            EmbeddedImage image,
            ICollection<ThreadGateRule>? threadGateRules = null,
            ICollection<PostGateRule>? postGateRules = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(text);

            if (text.Length > Maximum.PostLengthInCharacters || text.GetLengthInGraphemes() > Maximum.PostLengthInGraphemes)
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
                images = new List<EmbeddedImage>
                {
                    image
                };
            }

            return await Post(
                text,
                images: images,
                threadGateRules: threadGateRules,
                postGateRules: postGateRules,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bluesky post record.
        /// </summary>
        /// <param name="text">The text of the post record to create.</param>
        /// <param name="images">Any images to attach to the post.</param>
        /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
        /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
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
            CancellationToken cancellationToken = default)
        {
            EmbeddedImages? embeddedImages = null;

            ArgumentNullException.ThrowIfNullOrWhiteSpace(text);

            if (text.Length > Maximum.PostLengthInCharacters || text.GetLengthInGraphemes() > Maximum.PostLengthInGraphemes)
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

            NewPostRecord postRecord = new(
                text,
                langs : new List<string>() { Thread.CurrentThread.CurrentUICulture.Name },
                embed : embeddedImages);

            return await CreatePostWithGates(postRecord, Did, threadGateRules, postGateRules, cancellationToken).ConfigureAwait(false);
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

            NewPostRecord postRecord;
            List<ThreadGateRule>? threadGateRules = null;
            List<PostGateRule>? postGateRules = null;

            lock (postBuilder)
            {
                postRecord = postBuilder.ToPostRecord();

                if (postBuilder.ThreadGateRules is not null)
                {
                    threadGateRules = new List<ThreadGateRule>(postBuilder.ThreadGateRules);
                }

                if (postBuilder.PostGateRules is not null)
                {
                    postGateRules = new List<PostGateRule>(postBuilder.PostGateRules);
                }
            }

            return await CreatePostWithGates(
                postRecord,
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

            if (text.Length > Maximum.PostLengthInCharacters || text.GetLengthInGraphemes() > Maximum.PostLengthInGraphemes)
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

            if (text.Length > Maximum.PostLengthInCharacters || text.GetLengthInGraphemes() > Maximum.PostLengthInGraphemes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(text),
                    $"text cannot have a be longer than than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            List<EmbeddedImage> images = new()
            {
                image
            };

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

            if (text.Length > Maximum.PostLengthInCharacters || text.GetLengthInGraphemes() > Maximum.PostLengthInGraphemes)
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

            NewRepostRecord repostRecord = new(post);

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

            NewLikeRecord likeRecord = new(strongReference);

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
        /// <exception cref="ArgumentNullException">if <paramref name="strongReference"/>, <paramref name="text"/> is null or empty or <paramref name="image"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Quote(StrongReference strongReference, string text, EmbeddedImage image, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);
            ArgumentNullException.ThrowIfNullOrEmpty(text);
            ArgumentNullException.ThrowIfNull(image);

            return await Quote(strongReference, text, new List<EmbeddedImage>() { image }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, if any, and <paramref name="images" />, quoting the post identified by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
        /// <param name="text">The text for the new post</param>
        /// <param name="images">Any images to attach to the post.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="strongReference"/> is null or <paramref name="text"/> is null or empty.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Quote(StrongReference strongReference, string text, ICollection<EmbeddedImage>? images, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);
            ArgumentNullException.ThrowIfNull(text);

            if (text.Length > Maximum.PostLengthInCharacters || text.GetLengthInGraphemes() > Maximum.PostLengthInGraphemes)
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
                QuotePost = strongReference
            };

            postBuilder.Text = text;
            postBuilder.Languages = new List<string>(){ Thread.CurrentThread.CurrentUICulture.Name };

            if (images is not null)
            {
                postBuilder.Add(images);
            }

            return await Post(postBuilder, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an Bluesky post record  quoting the post identified by <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="strongReference"/> is null</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> Quote(StrongReference strongReference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            // This is a special case, it cannot go through the normal post APIs, it must go through the repo.ApplyWrites() api.

            NewPostRecord postRecord = new()
            {
                Embed = new EmbeddedRecord(strongReference),
                Text = string.Empty,
                CreatedAt = DateTimeOffset.UtcNow
            };

            ApplyWritesCreate applyWritesCreate = new(CollectionNsid.Post, TimestampIdentifier.Generate(), postRecord);

            AtProtoHttpResult<ApplyWritesResponse> result = await ApplyWrites(
                new List<ApplyWritesRequestValueBase>()
                {
                    applyWritesCreate
                },
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
                    result.AtErrorDetail,
                    result.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<CreateRecordResponse>(
                    null,
                    result.StatusCode,
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
        /// Uploads an an image to be referenced in a post.
        /// </summary>
        /// <param name="imageAsBytes">The image, as a byte array.</param>
        /// <param name="mimeType">The mime type of the image. No validation is performed on this value.</param>
        /// <param name="altText">The AltText (for accessibility) for the image.</param>
        /// <param name="aspectRatio">The image's aspect ratio.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">if <paramref name="imageAsBytes"/> is null.</exception>
        /// <exception cref="ArgumentException">if <paramref name="imageAsBytes"/> has a zero length.or if <paramref name="mimeType"/> is null or empty.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">if the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<EmbeddedImage>> UploadImage(
            byte[] imageAsBytes,
            string mimeType,
            string altText,
            AspectRatio? aspectRatio,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imageAsBytes);
            ArgumentException.ThrowIfNullOrEmpty(mimeType);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (imageAsBytes.Length == 0)
            {
                throw new ArgumentException("length cannot be 0.", nameof(imageAsBytes));
            }

            AtProtoHttpResult<Blob> uploadResult = await UploadBlob(
                imageAsBytes,
                mimeType,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (uploadResult.Succeeded)
            {
                return new AtProtoHttpResult<EmbeddedImage>(
                    new EmbeddedImage(uploadResult.Result, altText, aspectRatio),
                    uploadResult.StatusCode,
                    uploadResult.AtErrorDetail,
                    uploadResult.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<EmbeddedImage>(
                    null,
                    uploadResult.StatusCode,
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

        private async Task<AtProtoHttpResult<CreateRecordResponse>> CreatePostWithGates(
            NewPostRecord postRecord,
            Did did,
            ICollection<ThreadGateRule>? threadGateRules,
            ICollection<PostGateRule>? postGateRules,
            CancellationToken cancellationToken)
        {
            if (threadGateRules is null && postGateRules is null)
            {
                return await CreateRecord(
                    postRecord,
                    CollectionNsid.Post,
                    did,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // To be atomic we have to use ApplyWrites() rather than CreateRecord()
                List<ApplyWritesRequestValueBase> writeRequests = new();

                // We need to generate a record key to hang it all together.
                RecordKey rKey = TimestampIdentifier.Generate();
                AtUri postUri = new($"at://{did}/{CollectionNsid.Post}/{rKey}");

                writeRequests.Add(new ApplyWritesCreate(CollectionNsid.Post, rKey, postRecord));

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
                    await ApplyWrites(writeRequests, did, validate: true, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (response.Succeeded)
                {
                    CreateRecordResponse? createRecordResponse = null;

                    foreach (ApplyWritesResultBase result in response.Result.Results)
                    {
                        if (result is ApplyWritesCreateResult createResult && createResult.Uri == postUri)
                        {
                            createRecordResponse = new CreateRecordResponse(postUri, createResult.Cid, validationStatus: createResult.ValidationStatus, commit: response.Result.Commit);
                            break;
                        }
                    }

                    return new AtProtoHttpResult<CreateRecordResponse>(createRecordResponse, response.StatusCode, response.AtErrorDetail, response.RateLimit);
                }
                else
                {
                    return new AtProtoHttpResult<CreateRecordResponse>(null, response.StatusCode, response.AtErrorDetail, response.RateLimit);
                }
            }
        }

    }
}
