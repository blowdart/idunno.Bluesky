// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.BotSdk;

/// <summary>Builds complete durable intents without publishing anything.</summary>
/// <remarks><para>Call factories from <see cref="IBotPolicy"/>. The runtime saves the returned batch before
/// advancing replay progress. Do not rebuild an intent during retries.</para></remarks>
public static class BotActions
{
    /// <summary>Builds a reply using the supplied complete post and its existing thread references.</summary>
    /// <param name="source">The source post's URI and CID.</param>
    /// <param name="sequence">The replay event sequence.</param>
    /// <param name="reply">The complete reply, including its fixed creation time, parent, and root.</param>
    /// <param name="actionKey">The stable identity for this reply within the source event.</param>
    /// <returns>The reply intent.</returns>
    public static BotAction Reply(
        StrongReference source,
        long sequence,
        Post reply,
        string actionKey = "default") =>
        Create("reply", source, sequence, reply, BotJson.Post, actionKey);

    /// <summary>Builds a like with a fixed record key and creation time.</summary>
    /// <param name="source">The post to like.</param>
    /// <param name="sequence">The replay event sequence.</param>
    /// <param name="createdAt">The fixed creation time, or <see langword="null"/> to choose the current UTC time.</param>
    /// <param name="actionKey">The stable identity for this like within the source event.</param>
    /// <returns>The like intent.</returns>
    public static BotAction Like(
        StrongReference source,
        long sequence,
        DateTimeOffset? createdAt = null,
        string actionKey = "default") =>
        Create("like", source, sequence, new Like(source, createdAt ?? DateTimeOffset.UtcNow, null), BotJson.Like, actionKey);

    /// <summary>Builds a repost with a fixed record key and creation time.</summary>
    /// <param name="source">The post to repost.</param>
    /// <param name="sequence">The replay event sequence.</param>
    /// <param name="createdAt">The fixed creation time, or <see langword="null"/> to choose the current UTC time.</param>
    /// <param name="actionKey">The stable identity for this repost within the source event.</param>
    /// <returns>The repost intent.</returns>
    public static BotAction Repost(
        StrongReference source,
        long sequence,
        DateTimeOffset? createdAt = null,
        string actionKey = "default") =>
        Create("repost", source, sequence, new Repost(source, createdAt ?? DateTimeOffset.UtcNow), BotJson.Repost, actionKey);

    /// <summary>Builds a private service-managed bookmark intent.</summary>
    /// <param name="source">The post to bookmark, including the original CID.</param>
    /// <param name="sequence">The replay event sequence.</param>
    /// <returns>The bookmark intent.</returns>
    /// <remarks><para>Bookmarks have no caller-selected remote record key. Recovery scans bookmarks by post URI;
    /// unlike repository records, this does not provide fixed-key duplicate protection.</para></remarks>
    public static BotAction Bookmark(
        StrongReference source,
        long sequence) =>
        Create("bookmark", source, sequence, new BookmarkIntent(source), BotJsonContext.Default.BookmarkIntent);

    /// <summary>Builds an intent to add the source post's author to a bot-owned curation list.</summary>
    /// <param name="source">The source post identifying the author.</param>
    /// <param name="sequence">The replay event sequence.</param>
    /// <param name="listUri">The existing curation list's AT URI, not a list-item URI or web URL.</param>
    /// <param name="createdAt">The fixed creation time, or <see langword="null"/> to choose the current UTC time.</param>
    /// <returns>The author-membership intent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="listUri"/> is <see langword="null"/>.</exception>
    /// <remarks><para>The fixed list-item key derives from list URI and author DID, so several matching posts by the
    /// same author do not create new membership records. This does not create, mute, block, or subscribe to a list.</para></remarks>
    public static BotAction AddAuthorToList(
        StrongReference source,
        long sequence,
        AtUri listUri,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(listUri);
        Did author = Author(source);
        string identity = StableListItemRecordKey(listUri, author);
        ListItemIntent item = new(
            listUri,
            author,
            createdAt ?? DateTimeOffset.UtcNow);
        BotAction action = Create(
            "list-item",
            source,
            sequence,
            item,
            BotJsonContext.Default.ListItemIntent,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(listUri.ToString()))));

        return action with { RecordKey = identity };
    }

    /// <summary>Creates a stable list-item record key in the required TID format.</summary>
    /// <param name="listUri">The list record's AT URI.</param>
    /// <param name="author">The author DID whose membership record is keyed.</param>
    /// <returns>A deterministic 13-character record key for this list and author pair.</returns>
    /// <remarks><para>A raw hexadecimal digest is not a valid rkey because list-item records require a TID.</para></remarks>
    private static string StableListItemRecordKey(
        AtUri listUri,
        Did author)
    {
        ReadOnlySpan<byte> hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{listUri}\n{author}"));
        ulong value = BinaryPrimitives.ReadUInt64BigEndian(hash) & 0x7fff_ffff_ffff_ffff;
        const string alphabet = "234567abcdefghijklmnopqrstuvwxyz";
        Span<char> characters = stackalloc char[13];

        for (int index = characters.Length - 1; index >= 0; index--)
        {
            characters[index] = alphabet[(int)(value & 31)];
            value >>= 5;
        }

        return TimestampIdentifier.Parse(new string(characters), CultureInfo.InvariantCulture).ToString();
    }

    /// <summary>Builds an application-defined action using explicitly supplied AOT-safe JSON metadata.</summary>
    /// <typeparam name="TPayload">The application's complete payload type.</typeparam>
    /// <param name="kind">The registered handler's stable name.</param>
    /// <param name="source">The source post's URI and CID.</param>
    /// <param name="sequence">The replay event sequence.</param>
    /// <param name="payload">The complete payload chosen before sending.</param>
    /// <param name="metadata">The generated JSON metadata for the payload.</param>
    /// <param name="actionKey">The stable identity within this source and kind.</param>
    /// <param name="payloadVersion">The registered handler's supported payload version.</param>
    /// <returns>The complete pending intent.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="kind"/> or <paramref name="actionKey"/> is blank or exceeds 128 characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sequence"/> or <paramref name="payloadVersion"/> is not positive.</exception>
    public static BotAction Create<TPayload>(
        string kind,
        StrongReference source,
        long sequence,
        TPayload payload,
        JsonTypeInfo<TPayload> metadata,
        string actionKey = "default",
        int payloadVersion = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionKey);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(payloadVersion);
        if (kind.Length > 128 || actionKey.Length > 128)
        {
            throw new ArgumentException("Action kinds and keys must not exceed 128 characters.");
        }

        return new BotAction(
            source.Uri,
            sequence,
            Author(source).ToString(),
            TimestampIdentifier.Next().ToString(),
            JsonSerializer.Serialize(payload, metadata),
            "pending",
            0,
            ActionKind: kind,
            ActionKey: actionKey,
            PayloadVersion: payloadVersion);
    }

    /// <summary>Extracts the source repository's DID without a network handle lookup.</summary>
    /// <param name="source">The source reference whose repository is the author's DID.</param>
    /// <returns>The author DID preserved in durable intent.</returns>
    private static Did Author(StrongReference source) => new(source.Uri.ToString().Split('/')[2]);
}

/// <summary>Preserves the original post URI and CID for service-managed bookmark creation.</summary>
/// <param name="Subject">The unchanged reference used for all creation attempts.</param>
internal sealed record BookmarkIntent(StrongReference Subject);

/// <summary>Preserves a list membership's complete record without depending on the SDK's internal list-item type.</summary>
internal sealed record ListItemIntent
{
    /// <summary>Initializes a new instance of the <see cref="ListItemIntent"/> class.</summary>
    /// <param name="list">The existing curation list URI.</param>
    /// <param name="subject">The source author's DID.</param>
    /// <param name="createdAt">The fixed record creation time chosen before sending.</param>
    public ListItemIntent(
        AtUri list,
        Did subject,
        DateTimeOffset createdAt)
    {
        List = list;
        Subject = subject;
        CreatedAt = createdAt;
    }

    /// <summary>Gets the existing list receiving the author membership.</summary>
    public AtUri List { get; init; }

    /// <summary>Gets the triggering post author's DID.</summary>
    public Did Subject { get; init; }

    /// <summary>Gets the record creation time saved before any send attempt.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the record discriminator preserved in saved and remotely created JSON.</summary>
    [JsonPropertyName("$type")]
    public string Type { get; init; } = "app.bsky.graph.listitem";
}
