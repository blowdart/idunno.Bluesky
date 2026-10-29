// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Feed;

/// <summary>
/// Recording indicating the view of the specified post is blocked.
/// </summary>
public sealed record BlockedPost : PostViewBase
{
    [JsonConstructor]
    internal BlockedPost(AtUri uri, bool blocked, BlockedAuthor author)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(author);
        Uri = uri;
        Blocked = blocked;
        Author = author;
    }

    /// <summary>
    /// Flag indicating the view of the post is blocked.
    /// </summary>
    /// <exception cref="ArgumentException">The value is not <see langword="true"/>.</exception>
    [JsonRequired]
    public bool Blocked
    {
        get;
        init
        {
            if (!value)
            {
                throw new ArgumentException("A blocked post must have its blocked flag set.", nameof(value));
            }

            field = value;
        }
    } = true;

    /// <summary>
    /// Gets the blocked author of the post.
    /// </summary>
    [JsonRequired]
    public BlockedAuthor Author { get; init; }

    /// <summary>
    /// The <see cref="AtUri"/> of the blocked post.
    /// </summary>
    [JsonRequired]
    public AtUri Uri { get; init; }
}
