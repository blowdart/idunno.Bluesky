// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Actor;

/// <summary>
/// A link shown on an actor's profile.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true,
                 UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(Link), RecordType.ActorLink)]
public record Link : BlueskyTimestampedRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Link"/> record.
    /// </summary>
    /// <param name="url">The destination URI.</param>
    /// <exception cref="ArgumentNullException">The destination is <see langword="null"/>.</exception>
    public Link(Uri url) : this(url, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Link"/> record.
    /// </summary>
    /// <param name="url">The destination URI for the link.</param>
    /// <param name="title">An optional title for the link.</param>
    /// <param name="icon">An optional uploaded icon for the destination site.</param>
    /// <param name="labels">Optional self-labels for the link.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The icon is not a PNG, JPEG or WebP image.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The title exceeds 40 graphemes or 320 UTF-8 bytes, or the icon exceeds 100,000 bytes.</exception>
    public Link(Uri url, string? title, Blob? icon, SelfLabels? labels)
        : this(url, title, icon, labels, DateTimeOffset.UtcNow)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Link"/> record.
    /// </summary>
    /// <param name="url">The destination URI for the link.</param>
    /// <param name="title">An optional title for the link.</param>
    /// <param name="icon">An optional uploaded icon for the destination site.</param>
    /// <param name="labels">Optional self-labels for the link.</param>
    /// <param name="createdAt">The date and time the link was created.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The icon is not a PNG, JPEG or WebP image.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The title exceeds 40 graphemes or 320 UTF-8 bytes, or the icon exceeds 100,000 bytes.</exception>
    [JsonConstructor]
    public Link(Uri url, string? title, Blob? icon, SelfLabels? labels, DateTimeOffset createdAt)
        : base(createdAt)
    {
        ArgumentNullException.ThrowIfNull(url);

        Url = url;
        Title = title;
        Icon = icon;
        Labels = labels;
    }

    /// <summary>
    /// Gets the destination URI for the link.
    /// </summary>
    [JsonRequired]
    public Uri Url { get; init; }

    /// <summary>
    /// Gets the optional title for the link.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The title exceeds 40 graphemes or 320 UTF-8 bytes.</exception>
    public string? Title
    {
        get;
        init
        {
            if (value is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetGraphemeLength(), 40);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetUtf8Length(), 320);
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets the optional uploaded icon for the destination site.
    /// </summary>
    /// <exception cref="ArgumentException">The icon is not a PNG, JPEG or WebP image.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The icon exceeds 100,000 bytes.</exception>
    public Blob? Icon
    {
        get;
        init
        {
            if (value is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Size, 100000);
                if (value.MimeType is not ("image/png" or "image/jpeg" or "image/webp"))
                {
                    throw new ArgumentException("The icon must be a PNG, JPEG or WebP image.", nameof(value));
                }
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets optional self-labels for the link.
    /// </summary>
    public SelfLabels? Labels { get; init; }
}
