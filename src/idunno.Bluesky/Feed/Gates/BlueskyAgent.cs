// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;

using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Feed.Gates.Model;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Creates a thread gate record in the current user's repository for the specified <paramref name="post"/>
        /// </summary>
        /// <param name="post">The <see cref="AtUri"/> of the thread to be gated.</param>
        /// <param name="allow">The list of rules for replies to the specified <paramref name="post"/>.</param>
        /// <param name="hiddenReplies">A list of reply <see cref="AtUris"/> that will be hidden for <see cref="Post"/></param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when <paramref name="post"/>has a null repo,  does not point to a post record or the current user does not own the record pointed to.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when <paramref name="allow"/> or <paramref name="hiddenReplies"/> have more than the maximum number of entries.
        /// </exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> AddThreadGate(
            AtUri post,
            ICollection<ThreadGateRule>? rules = null,
            ICollection<AtUri>? hiddenReplies = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (post.Repo is null)
            {
                throw new ArgumentException("Repo is null", nameof(post));
            }

            if (post.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException("Does not point to a Post record", nameof(post));
            }

            if (!await IsRecordOwnedBy(post, Did, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Post was not created by the current user.", nameof(post));
            }

            if (rules is not null && rules.Count != 0)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(rules.Count, Maximum.ThreadGateRules, nameof(rules));
            }

            if (hiddenReplies is not null && hiddenReplies.Count != 0)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(hiddenReplies.Count, Maximum.ThreadGateHiddenReplies, nameof(hiddenReplies));
            }

            ThreadGate threadGate = new (post, rules, hiddenReplies);

            return await AddThreadGate(threadGate, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates the specified <paramref name="threadGate"/> record in the current user's repository.
        /// </summary>
        /// <param name="threadGate">The <paramref name="threadGate"/> record to create.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="threadGate"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> AddThreadGate(
            ThreadGate threadGate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(threadGate);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (!await IsRecordOwnedBy(threadGate.Post, Did, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Post was not created by the current user.", nameof(threadGate));
            }

            return await CreateRecord(
                record: threadGate,
                collection: CollectionNsid.ThreadGate,
                Did,
                rkey: threadGate.Post.RecordKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a thread gate on the specified post, if one exists.
        /// </summary>
        /// <param name="post">The <paramref name="post"/> to delete the thread gate from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="post"/> does not point to a post record, or its RecordKey is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteThreadGate(
            AtUri post,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (post.RecordKey is null)
            {
                throw new ArgumentException("RecordKey is null", nameof(post));
            }

            if (post.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException("does not point to a Post record", nameof (post));
            }

            if (!await IsRecordOwnedBy(post, Did, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Post was not created by the current user.", nameof(post));
            }

            return await DeleteRecord(
                collection: CollectionNsid.ThreadGate,
                rKey: post.RecordKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the specified <paramref name="threadGate"/> record in the current user's repository.
        /// </summary>
        /// <param name="threadGate">The <paramref name="threadGate"/> record to update.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="threadGate"/> is null.</exception>
        /// <exception cref="ArgumentException">
        ///     Thrown when the <paramref name="threadGate"/>'s RecordKey is null, or it does not point to a Post record or it does not belong to the current user.
        /// </exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<PutRecordResponse>> UpdateThreadGate(
            ThreadGate threadGate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(threadGate);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (threadGate.Post.RecordKey is null)
            {
                throw new ArgumentException("Post.RecordKey is null", nameof(threadGate));
            }

            if (threadGate.Post.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException("Post does not point to a Post record", nameof(threadGate));
            }

            if (!await IsRecordOwnedBy(threadGate.Post, Did, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Post was not created by the current user.", nameof(threadGate));
            }

            return await PutRecord(
                record: threadGate,
                collection: CollectionNsid.ThreadGate,
                creator: Did,
                rkey: threadGate.Post.RecordKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the <see cref="ThreadGate"/> for the specified <paramref name="post"/>, if any.
        /// </summary>
        /// <param name="post">The <see cref="AtUri"/> for the post whose thread gate should be retrieved.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="post"/> does not point to a post record, or its RecordKey is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<ThreadGate>> GetThreadGate(
            AtUri post,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (post.RecordKey is null)
            {
                throw new ArgumentException("RecordKey is null", nameof(post));
            }

            if (post.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException("Does not point to a Post record", nameof(post));
            }

            AtProtoHttpResult<GetThreadGateResponse> recordResponse = await GetRecord<GetThreadGateResponse>(
                repo: Did,
                collection: CollectionNsid.ThreadGate,
                rKey: post.RecordKey,
                cid: null,
                Service,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (recordResponse.Succeeded)
            {
                return new AtProtoHttpResult<ThreadGate>(
                    recordResponse.Result.Value,
                    recordResponse.StatusCode,
                    recordResponse.AtErrorDetail,
                    recordResponse.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ThreadGate>(
                    null,
                    recordResponse.StatusCode,
                    recordResponse.AtErrorDetail,
                    recordResponse.RateLimit);
            }
        }

        /// <summary>
        /// Creates the specified <paramref name="postGate"/> record in the current user's repository.
        /// </summary>
        /// <param name="threadGate">The <paramref name="threadGate"/> record to create.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="threadGate"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> AddPostGate(
            AtUri post,
            ICollection<PostGateRule>? rules = null,
            ICollection<AtUri>? detachedEmbeddingUris = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (!await IsRecordOwnedBy(post, Did, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Post was not created by the current user.", nameof(post));
            }

            if (rules is not null && rules.Count != 0)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(rules.Count, Maximum.PostGateRules, nameof(rules));
            }

            if (detachedEmbeddingUris is not null && detachedEmbeddingUris.Count != 0)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(detachedEmbeddingUris.Count, Maximum.PostGateDetachedEmbeddingPosts, nameof(detachedEmbeddingUris));
            }

            return await AddPostGate(
                new PostGate(
                    post,
                    rules,
                    detachedEmbeddingUris),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates the specified <paramref name="postGate"/> record in the current user's repository.
        /// </summary>
        /// <param name="threadGate">The <paramref name="threadGate"/> record to create.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="threadGate"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> AddPostGate(
            PostGate postGate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(postGate);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await CreateRecord(
                record: postGate,
                collection: CollectionNsid.PostGate,
                Did,
                rkey: postGate.Post.RecordKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a post gate on the specified post, if one exists.
        /// </summary>
        /// <param name="post">The <paramref name="post"/> to delete the post gate from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="post"/> does not point to a post record, or its RecordKey is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeletePostGate(
            AtUri post,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (post.RecordKey is null)
            {
                throw new ArgumentException("RecordKey is null", nameof(post));
            }

            if (post.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException("does not point to a Post record", nameof(post));
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (!await IsRecordOwnedBy(post, Did, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Post was not created by the current user.", nameof(post));
            }

            return await DeleteRecord(
                collection: CollectionNsid.PostGate,
                rKey: post.RecordKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the <see cref="PostGate"/> for the specified <paramref name="post"/>, if any.
        /// </summary>
        /// <param name="post">The <see cref="AtUri"/> for the post whose post gate should be retrieved.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="post"/> does not point to a post record, or its RecordKey is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<PostGate>> GetPostGate(
            AtUri post,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (post.RecordKey is null)
            {
                throw new ArgumentException("RecordKey is null", nameof(post));
            }

            if (post.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException("Does not point to a Post record", nameof(post));
            }

            AtProtoHttpResult<GetPostGateResponse> recordResponse = await GetRecord<GetPostGateResponse>(
                repo: Did,
                collection: CollectionNsid.PostGate,
                rKey: post.RecordKey,
                cid: null,
                Service,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (recordResponse.Succeeded)
            {
                return new AtProtoHttpResult<PostGate>(
                    recordResponse.Result.Value,
                    recordResponse.StatusCode,
                    recordResponse.AtErrorDetail,
                    recordResponse.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PostGate>(
                    null,
                    recordResponse.StatusCode,
                    recordResponse.AtErrorDetail,
                    recordResponse.RateLimit);
            }
        }

        /// <summary>
        /// Updates the specified <paramref name="postGate"/> record in the current user's repository.
        /// </summary>
        /// <param name="postGate">The <paramref name="postGate"/> record to update.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="postGate"/> is null.</exception>
        /// <exception cref="ArgumentException">
        ///     Thrown when the <paramref name="postGate"/>'s RecordKey is null, or it does not point to a Post record or it does not belong to the current user.
        /// </exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<PutRecordResponse>> UpdatePostGate(
            PostGate postGate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(postGate);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (postGate.Post.RecordKey is null)
            {
                throw new ArgumentException("Post.RecordKey is null", nameof(postGate));
            }

            if (postGate.Post.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException("Post does not point to a Post record", nameof(postGate));
            }

            if (!await IsRecordOwnedBy(postGate.Post, Did, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("Post was not created by the current user.", nameof(postGate));
            }

            return await PutRecord(
                record: postGate,
                collection: CollectionNsid.PostGate,
                creator: Did,
                rkey: postGate.Post.RecordKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> IsRecordOwnedBy(AtUri uri, Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(did);

            Did? resolvedDid = null;

            if (uri.Repo is Handle handle)
            {
                resolvedDid = await ResolveHandle(handle, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else if (uri.Repo is Did uriDid)
            {
                resolvedDid = uriDid;
            }

            return resolvedDid is not null && resolvedDid == did;
        }
    }
}
