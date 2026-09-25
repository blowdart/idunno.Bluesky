// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.Bluesky.Embed;
using idunno.Bluesky.RichText;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Presents a view over a message.
/// </summary>
public sealed record MessageView : MessageViewBase
{
    /// <summary>
    /// Creates a new instance of <see cref="MessageView"/>.
    /// </summary>
    /// <param name="id">The message ID.</param>
    /// <param name="revision">The message revision.</param>
    /// <param name="text">The text of the message.</param>
    /// <param name="facets">Facets to apply to the message <paramref name="text"/>, if any.</param>
    /// <param name="embed">A view over the embedded record in the message, if any.</param>
    /// <param name="reactions">Reactions to the message <paramref name="text"/>, if any.</param>
    /// <param name="sender">A view over the message author.</param>
    /// <param name="sentAt">The <see cref="DateTimeOffset"/> the message was sent on.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown when either <paramref name="id" /> or <paramref name="revision"/> is <see langword="null"/> or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="text"/> or <paramref name="sender"/> is <see langword="null"/>.
    /// </exception>
    public MessageView(
        string id,
        string revision,
        string text,
        IReadOnlyCollection<Facet>? facets,
        EmbeddedRecordView? embed,
        IReadOnlyCollection<ReactionView>? reactions,
        MessageViewSender sender,
        DateTimeOffset sentAt) : base()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sender);

        Id = id;
        Revision = revision;
        SentAt = sentAt;

        Text = text;
        Embed = embed;
        Sender = sender;

        Facets = facets ?? [];
        Reactions = reactions ?? [];
    }

    /// <summary>
    /// Gets the id of a message.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string Id { get; init; }

    /// <summary>
    /// Gets the revision of a message.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("rev")]
    public string Revision { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> the message was sent on.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset SentAt { get; init; }

    /// <summary>
    /// Gets the text of a message.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string Text { get; init; }

    /// <summary>
    /// Gets any facets to apply to <see cref="Text"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value supplied is copied, so later changes to the collection assigned are not reflected here.
    /// A <see langword="null"/> value is normalised to an empty collection.
    /// </para>
    /// </remarks>
    [JsonInclude]
    [NotNull]
    public IReadOnlyCollection<Facet> Facets
    {
        get => _facets;
        init => _facets = value is null ? new List<Facet>().AsReadOnly() : new List<Facet>(value).AsReadOnly();
    }

    /// <summary>
    /// Gets a view over the embedded record, if any.
    /// </summary>
    [JsonInclude]
    public EmbeddedRecordView? Embed { get; init; }

    /// <summary>
    /// Gets reactions to the message, in ascending order of creation time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value supplied is copied, so later changes to the collection assigned are not reflected here.
    /// A <see langword="null"/> value is normalised to an empty collection.
    /// </para>
    /// </remarks>
    [JsonInclude]
    [NotNull]
    public IReadOnlyCollection<ReactionView> Reactions
    {
        get => _reactions;
        init => _reactions = value is null ? new List<ReactionView>().AsReadOnly() : new List<ReactionView>(value).AsReadOnly();
    }

    /// <summary>
    /// Gets a view over the message author.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public MessageViewSender Sender { get; init; }

    private readonly IReadOnlyCollection<Facet> _facets = null!;

    private readonly IReadOnlyCollection<ReactionView> _reactions = null!;
}