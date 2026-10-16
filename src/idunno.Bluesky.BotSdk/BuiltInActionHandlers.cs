// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.BotSdk;

/// <summary>Creates and reconciles complete replies with fixed record keys and thread references.</summary>
public sealed class ReplyActionHandler : IBotActionHandler
{
    /// <inheritdoc/>
    public string Kind => "reply";

    /// <inheritdoc/>
    public int PayloadVersion => 1;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The saved reply is invalid or does not reference its source post.</exception>
    public void Validate(BotAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Post post = ActionRecords.Read(action, BotJson.Post);
        if (post.Reply is null || post.Reply.Parent.Uri != action.SourceUri)
        {
            throw new InvalidDataException("A reply must reference its source post as parent and preserve the thread root.");
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="remote"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The existing reply differs from the saved intent.</exception>
    public async Task<BotActionResult?> ReconcileAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(remote);
        Post expected = ActionRecords.Read(action, BotJson.Post);
        if (action.Status == "uncertain")
        {
            string? found = await remote.FindRecordAsync(action, cancellationToken).ConfigureAwait(false);
            if (found is not null)
            {
                Post actual = JsonSerializer.Deserialize(found, BotJson.Post)
                    ?? throw new InvalidDataException("The existing reply is null.");
                if (!ReplyRecords.SameRecord(actual, expected))
                {
                    throw new InvalidDataException("The existing reply differs from the durable intent. Refusing to overwrite it.");
                }

                return ActionRecords.Reconciled(action, remote);
            }
        }
        if (action.TargetDeleted)
        {
            return null;
        }
        foreach (AtUri uri in new[] { expected.Reply!.Parent.Uri, expected.Reply.Root.Uri }.Distinct())
        {
            PostView target = await remote.TargetAsync(uri, cancellationToken).ConfigureAwait(false);
            if (target.Viewer?.ReplyDisabled == true)
            {
                return new BotActionResult(false, null, "server-confirmed reply restriction");
            }
        }

        return null;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="remote"/> is <see langword="null"/>.</exception>
    public Task<BotActionResult> ExecuteAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(remote);

        return remote.CreateRecordAsync(action, cancellationToken);
    }
}

/// <summary>Creates and reconciles likes while respecting an existing viewer like.</summary>
public sealed class LikeActionHandler : IBotActionHandler
{
    /// <inheritdoc/>
    public string Kind => "like";

    /// <inheritdoc/>
    public int PayloadVersion => 1;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The saved like is invalid or references a different post.</exception>
    public void Validate(BotAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ActionRecords.CheckSubject(action, ActionRecords.Read(action, BotJson.Like).Subject.Uri);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="remote"/> is <see langword="null"/>.</exception>
    public async Task<BotActionResult?> ReconcileAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(remote);
        BotActionResult? existing = await ActionRecords.ReconcileAsync(action, remote, cancellationToken).ConfigureAwait(false);
        if (existing is not null || action.TargetDeleted)
        {
            return existing;
        }
        PostView target = await remote.TargetAsync(action.SourceUri, cancellationToken).ConfigureAwait(false);

        return target.Viewer?.Like is AtUri uri
            ? new BotActionResult(true, uri.ToString(), "already liked")
            : null;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="remote"/> is <see langword="null"/>.</exception>
    public Task<BotActionResult> ExecuteAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(remote);

        return remote.CreateRecordAsync(action, cancellationToken);
    }
}

/// <summary>Creates and reconciles reposts while respecting an existing viewer repost.</summary>
public sealed class RepostActionHandler : IBotActionHandler
{
    /// <inheritdoc/>
    public string Kind => "repost";

    /// <inheritdoc/>
    public int PayloadVersion => 1;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The saved repost is invalid or references a different post.</exception>
    public void Validate(BotAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ActionRecords.CheckSubject(action, ActionRecords.Read(action, BotJson.Repost).Subject.Uri);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="remote"/> is <see langword="null"/>.</exception>
    public async Task<BotActionResult?> ReconcileAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(remote);
        BotActionResult? existing = await ActionRecords.ReconcileAsync(action, remote, cancellationToken).ConfigureAwait(false);
        if (existing is not null || action.TargetDeleted)
        {
            return existing;
        }
        PostView target = await remote.TargetAsync(action.SourceUri, cancellationToken).ConfigureAwait(false);

        return target.Viewer?.Repost is AtUri uri
            ? new BotActionResult(true, uri.ToString(), "already reposted")
            : null;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="remote"/> is <see langword="null"/>.</exception>
    public Task<BotActionResult> ExecuteAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(remote);

        return remote.CreateRecordAsync(action, cancellationToken);
    }
}

/// <summary>Creates private bookmarks and reconciles them by scanning all bookmark pages.</summary>
/// <remarks><para>Bookmarks are service-managed, not fixed-key repository records. A scan and create cannot be atomic;
/// concurrent account use or delayed read visibility can still race with this action.</para></remarks>
public sealed class BookmarkActionHandler : IBotActionHandler
{
    /// <inheritdoc/>
    public string Kind => "bookmark";

    /// <inheritdoc/>
    public int PayloadVersion => 1;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The saved bookmark intent is invalid or references a different post.</exception>
    public void Validate(BotAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ActionRecords.CheckSubject(action, ActionRecords.Read(action, BotJsonContext.Default.BookmarkIntent).Subject.Uri);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="remote"/> is <see langword="null"/>.</exception>
    public async Task<BotActionResult?> ReconcileAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(remote);
        bool exists = await remote.HasBookmarkAsync(action.SourceUri, cancellationToken).ConfigureAwait(false);

        return exists
            ? new BotActionResult(true, action.SourceUri.ToString(), "bookmark already exists")
            : null;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="remote"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The saved bookmark intent is <see langword="null"/> or invalid.</exception>
    public async Task<BotActionResult> ExecuteAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(remote);
        BookmarkIntent intent = ActionRecords.Read(action, BotJsonContext.Default.BookmarkIntent);
        await remote.CreateBookmarkAsync(intent.Subject, cancellationToken).ConfigureAwait(false);

        return new BotActionResult(true, action.SourceUri.ToString(), "bookmark created");
    }
}

/// <summary>Adds the source author to an owned curation list without deliberately duplicating membership.</summary>
/// <remarks><para>Uses a stable list-and-author record key and also checks existing manual memberships.
/// This action does not create a list or enable moderation, muting, or blocking.</para></remarks>
public sealed class ListItemActionHandler : IBotActionHandler
{
    /// <inheritdoc/>
    public string Kind => "list-item";

    /// <inheritdoc/>
    public int PayloadVersion => 1;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The saved list membership is invalid or identifies a different author or list.</exception>
    public void Validate(BotAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ListItemIntent item = ActionRecords.Read(action, BotJsonContext.Default.ListItemIntent);
        if (item.Subject.ToString() != action.SourceDid ||
            !item.List.ToString().Contains("/app.bsky.graph.list/", StringComparison.Ordinal))
        {
            throw new InvalidDataException("An author-list action must identify its source author and a list URI.");
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="remote"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The saved or existing list membership is invalid or conflicts with the durable intent.</exception>
    public async Task<BotActionResult?> ReconcileAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(remote);
        ListItemIntent expected = ActionRecords.Read(action, BotJsonContext.Default.ListItemIntent);
        string? found = await remote.FindRecordAsync(action, cancellationToken).ConfigureAwait(false);
        if (found is not null)
        {
            ListItemIntent actual = JsonSerializer.Deserialize(found, BotJsonContext.Default.ListItemIntent)
                ?? throw new InvalidDataException("The existing list item is null.");
            // Membership is the list/author pair; a later post may choose a different creation time.
            if (actual.List != expected.List || actual.Subject != expected.Subject)
            {
                throw new InvalidDataException("The fixed list-item URI contains a different membership.");
            }

            return ActionRecords.Reconciled(action, remote);
        }
        string? membership = await remote.FindListMemberAsync(expected.List, expected.Subject, cancellationToken).ConfigureAwait(false);

        return membership is null
            ? null
            : new BotActionResult(true, membership, "author already belongs to list");
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="remote"/> is <see langword="null"/>.</exception>
    public Task<BotActionResult> ExecuteAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(remote);

        return remote.CreateRecordAsync(action, cancellationToken);
    }
}

/// <summary>Shares generated payload decoding and fixed-record reconciliation across built-in handlers.</summary>
internal static class ActionRecords
{
    /// <summary>Decodes the saved payload without reflection-based serialization or null fallbacks.</summary>
    /// <typeparam name="T">The payload type supported by generated metadata.</typeparam>
    /// <param name="action">The durable action envelope.</param>
    /// <param name="metadata">The generated payload contract.</param>
    /// <returns>The decoded non-null payload.</returns>
    /// <exception cref="InvalidDataException">The saved payload is null.</exception>
    public static T Read<T>(BotAction action, JsonTypeInfo<T> metadata) =>
        JsonSerializer.Deserialize(action.RecordJson, metadata)
        ?? throw new InvalidDataException("A durable action payload is null.");

    /// <summary>Rejects payloads targeting a different post from the saved source identity.</summary>
    /// <param name="action">The envelope identifying the triggering post.</param>
    /// <param name="uri">The subject referenced by the decoded payload.</param>
    /// <exception cref="InvalidDataException">The subject does not match the source URI.</exception>
    public static void CheckSubject(BotAction action, AtUri uri)
    {
        if (uri != action.SourceUri)
        {
            throw new InvalidDataException("The saved action must reference its source post.");
        }
    }

    /// <summary>Maps a built-in repository action to its AT Protocol collection.</summary>
    /// <param name="kind">The durable action kind.</param>
    /// <returns>The collection NSID for the repository record.</returns>
    /// <exception cref="InvalidOperationException">The kind has no built-in repository collection.</exception>
    public static string Collection(string kind) => kind switch
    {
        "reply" => "app.bsky.feed.post",
        "like" => "app.bsky.feed.like",
        "repost" => "app.bsky.feed.repost",
        "list-item" => "app.bsky.graph.listitem",
        _ => throw new InvalidOperationException($"Action '{kind}' has no built-in repository collection.")
    };

    /// <summary>Builds the saved record's fixed URI in the authenticated bot's repository.</summary>
    /// <param name="action">The kind and persisted record key.</param>
    /// <param name="remote">The transport exposing the expected bot DID.</param>
    /// <returns>The fixed remote URI used for creation and recovery.</returns>
    public static AtUri Uri(BotAction action, IBotActionRemote remote) =>
        new($"at://{remote.BotDid}/{Collection(action.ActionKind)}/{action.RecordKey}");

    /// <summary>Describes a verified existing effect without claiming a new remote write occurred.</summary>
    /// <param name="action">The verified saved action.</param>
    /// <param name="remote">The transport identifying the bot's repository.</param>
    /// <returns>The successful reconciliation outcome at the fixed URI.</returns>
    public static BotActionResult Reconciled(BotAction action, IBotActionRemote remote) =>
        new(true, Uri(action, remote).ToString(), "reconciled existing record");

    /// <summary>Verifies uncertain likes and reposts before any retry can overwrite their fixed URI.</summary>
    /// <param name="action">The saved complete intent and attempt status.</param>
    /// <param name="remote">The remote record reader.</param>
    /// <param name="cancellationToken">The active ownership/shutdown token.</param>
    /// <returns>The confirmed existing effect, or <see langword="null"/> when execution is still needed.</returns>
    /// <exception cref="InvalidDataException">The remote record differs from the saved intent.</exception>
    public static async Task<BotActionResult?> ReconcileAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        if (action.Status != "uncertain")
        {
            return null;
        }
        string? found = await remote.FindRecordAsync(action, cancellationToken).ConfigureAwait(false);
        if (found is null)
        {
            return null;
        }
        using JsonDocument actual = JsonDocument.Parse(found);
        using JsonDocument expected = JsonDocument.Parse(action.RecordJson);
        if (!JsonElement.DeepEquals(actual.RootElement, expected.RootElement))
        {
            throw new InvalidDataException("The existing record differs from the durable intent. Refusing to overwrite it.");
        }

        return Reconciled(action, remote);
    }
}
