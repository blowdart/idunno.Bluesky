// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="id" /> or <paramref name="revision"/> is <see langword="null"/> or whitespace.
    /// </exception>
    public SystemMessageView(
        SystemMessage data,
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
    public SystemMessage Data { get; internal init; }
}