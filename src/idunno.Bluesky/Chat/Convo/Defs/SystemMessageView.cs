// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Presents a view over a system message.
/// </summary>
public sealed record SystemMessageView : MessageViewBase
{
    /// <summary>
    /// Creates a new instance of <see cref="SystemMessageView"/>.
    /// </summary>
    /// <param name="data">The system message data.</param>
    /// <param name="id">The message ID.</param>
    /// <param name="revision">The message revision.</param>
    /// <param name="sentAt">The <see cref="DateTimeOffset"/> the message was sent on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/>, <paramref name="revision"/> or <paramref name="data"/> is <see langword="null"/>.</exception>
    public SystemMessageView(
        SystemMessage data,
        string id,
        string revision,
        DateTimeOffset sentAt) : base()
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(data);

        Id = id;
        Revision = revision;
        SentAt = sentAt;
        Data = data;
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
    /// Gets the system message data.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public SystemMessage Data { get; init; }
}