// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Chat;

/// <summary>
/// Implements a paged collection of messages from a direct message conversation.
/// </summary>
/// <param name="list">The list of <see cref="MessageViewBase"/> to create this instance of <see cref="Messages"/> from.</param>
/// <param name="cursor">An optional cursor for pagination.</param>
/// <remarks>
/// <para>Creates a new instance of <see cref="Messages"/>.</para>
/// </remarks>
public class Messages(IList<MessageViewBase> list, string? cursor = null) : PagedViewReadOnlyCollection<MessageViewBase>(list, cursor)
{
    /// <summary>
    /// Creates a new instance of <see cref="Messages"/> with an empty list and no cursor.
    /// </summary>
    public Messages() : this([], null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Messages"/>.
    /// </summary>
    /// <param name="collection">A collection of <see cref="MessageViewBase"/> to create this instance of <see cref="Messages"/> from.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    public Messages(ICollection<MessageViewBase> collection, string? cursor = null) : this([.. collection], cursor)
    {
    }
}
