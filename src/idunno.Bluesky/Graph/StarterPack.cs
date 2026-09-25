// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Graph;

/// <summary>
///  Encapsulates a Bluesky starter pack.
/// </summary>
[JsonPolymorphic(
    IgnoreUnrecognizedTypeDiscriminators = true,
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(StarterPack), typeDiscriminator: RecordType.StarterPack)]
public record StarterPack : BlueskyTimestampedRecord
{
    private string _name = null!;
    private string? _description;
    private AtUri _list = null!;

    /// <summary>
    /// Creates a new instance of <see cref="StarterPack"/>.
    /// </summary>
    /// <param name="name">The name of the starter pack.</param>
    /// <param name="description">The description of the starter pack.</param>
    /// <param name="list">The <see cref="AtUri"/> of the starter pack.</param>
    /// <param name="feeds">A collection of <see cref="GeneratorView"/>s for any feeds in the starter pack.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the starter pack was created on.</param>
    /// <param name="updatedAt">The <see cref="DateTimeOffset"/> the starter pack was last updated.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="list"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="name"/> is &gt; 50 graphemes or &gt; 500 UTF-8 bytes,
    /// if <paramref name="description"/> is &gt; 300 graphemes or &gt; 3000 UTF-8 bytes,
    /// or if more than 3 feeds are provided in <paramref name="feeds"/>.
    /// </exception>
    [JsonConstructor]
    public StarterPack(
        string name,
        string? description,
        AtUri list,
        IReadOnlyList<FeedItem>? feeds,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt) : base(createdAt)
    {
        ArgumentNullException.ThrowIfNull(list);

        Name = name;
        Description = description;
        List = list;

        if (feeds is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(feeds.Count, Maximum.FeedsInStarterPack);
        }
        Feeds = feeds;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Gets the name of the starter pack.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> exceeds 50 graphemes or 500 UTF-8 bytes.</exception>
    [JsonInclude]
    [JsonRequired]
    public string Name
    {
        get
        {
            return _name;
        }

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetGraphemeLength(), Maximum.StarterPackNameLengthInGraphemes);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetUtf8Length(), Maximum.StarterPackNameLengthInBytes);

            _name = value;
        }
    }

    /// <summary>
    /// Gets the description of the starter pack
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value exceeds 300 graphemes or 3000 UTF-8 bytes.</exception>
    [JsonInclude]
    public string? Description
    {
        get
        {
            return _description;
        }

        init
        {
            if (value is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetGraphemeLength(), Maximum.StarterPackDescriptionLengthInGraphemes);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetUtf8Length(), Maximum.StarterPackDescriptionLengthInBytes);
            }

            _description = value;
        }
    }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the starter pack.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    [JsonInclude]
    public AtUri List
    {
        get
        {
            return _list;
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _list = value;
        }
    }

    /// <summary>
    /// Gets a collection of <see cref="GeneratorView"/>s for any feeds in the starter pack.
    /// </summary>
    [JsonInclude]
    public IReadOnlyList<FeedItem>? Feeds { get; init; }

    /// <summary>
    /// The <see cref="DateTimeOffset"/> the starter pack was last updated.
    /// </summary>
    [JsonInclude]
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>
/// Represents a reference to a feed.
/// </summary>
public record FeedItem
{
    /// <summary>
    /// Creates a new instance of <see cref="FeedItem"/>.
    /// </summary>
    /// <param name="uri">The feed URI</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null"/>.</exception>.
    [JsonConstructor]
    public FeedItem(AtUri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Uri = uri;
    }

    /// <summary>
    /// The AT URI of the feed.
    /// </summary>
    [JsonRequired]
    public AtUri Uri { get; init; }
}