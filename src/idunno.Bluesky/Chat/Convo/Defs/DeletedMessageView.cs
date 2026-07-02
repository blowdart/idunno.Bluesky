// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Presents a view over a message that has been deleted.
/// </summary>
public sealed record DeletedMessageView : MessageViewBase
{
    /// <summary>
    /// Creates a new instance of <see cref="DeletedMessageView"/>.
    /// </summary>
    /// <param name="id">The message ID.</param>
    /// <param name="revision">The message revision.</param>
    /// <param name="sender">A view over the message author.</param>
    /// <param name="sentAt">The <see cref="DateTimeOffset"/> the message was sent on.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="id" /> or <paramref name="revision"/> is <see langword="null"/> or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="sender"/> is <see langword="null"/>.
    /// </exception>
    public DeletedMessageView(
        string id,
        string revision,
        MessageViewSender sender,
        DateTimeOffset sentAt) : base()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        ArgumentNullException.ThrowIfNull(sender);

        Id = id;
        Revision = revision;
        SentAt = sentAt;
        Sender = sender;
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
    /// Gets a view over the message author.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public MessageViewSender Sender { get; init; }
}