// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Record;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a like record in the current user's repo for the record pointed to by the <paramref name="strongReference"/>.
    /// </summary>
    /// <param name="strongReference">A <see cref="StrongReference"/> to the record to be liked.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> or its <see cref="StrongReference.Uri"/> collection is <see langword="null"/>.</exception>
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

        Feed.Like likeRecord = new(strongReference);

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, the uri collection, or <paramref name="cid"/> is <see langword="null"/>.</exception>
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postView"/>, the <see cref="PostView.Uri"/> property, or the <see cref="PostView.Uri"/> collection is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="postView"/> Uri does not point to a post.</exception>
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/>, or its <see cref="FeedViewPost.Post"/> property, or it's collection is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when the <see cref="FeedViewPost.Post"/> property on <paramref name="post"/> does not point to a post, or.
    ///   if a reason is present and the reason is typeof <see cref="ReasonRepost"/> and reason <see cref="StrongReference"/> is <see langword="null"/>, or the strong reference's
    ///   Uri property is <see langword="null"/>, or the strong reference's URI does not point to a Repost record.
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

        Feed.Like likeRecord;

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
    private async Task<AtProtoHttpResult<CreateRecordResult>> Like(Feed.Like likeRecord, CancellationToken cancellationToken = default)
    {
        // We use the BlueskyTimestampedRecordValue class as the generic so the type discriminator appears in the serialized output.
        return await CreateRecord<BlueskyTimestampedRecord>(
            record: likeRecord,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.Like,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
