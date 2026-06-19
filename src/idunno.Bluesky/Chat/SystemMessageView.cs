// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Chat.SystemMessages;

namespace idunno.Bluesky.Chat;

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
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="id" /> or <paramref name="revision"/> is <see langword="null"/> or whitespace.
    /// </exception>
    public SystemMessageView(
        SystemMessageBase data,
        string id,
        string revision,
        DateTimeOffset sentAt) : base(id, revision, sentAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        ArgumentNullException.ThrowIfNull(data);
        Data = data;
    }

    /// <summary>
    /// Gets the system message data.
    /// </summary>
    public SystemMessageBase Data { get; init; }
}