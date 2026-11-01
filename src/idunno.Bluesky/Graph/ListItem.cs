// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Graph;

/// <summary>
/// Represents an actor's membership in a list.
/// </summary>
[JsonPolymorphic(
    IgnoreUnrecognizedTypeDiscriminators = true,
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(ListItem), typeDiscriminator: RecordType.ListItem)]
public record ListItem : BlueskyTimestampedRecord
{
    private AtUri _list = null!;
    private Did _subject = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListItem"/> record.
    /// </summary>
    /// <remarks>
    /// <para>The required <see cref="List"/> and <see cref="Subject"/> properties must be initialized.</para>
    /// </remarks>
    public ListItem()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ListItem"/> record.
    /// </summary>
    /// <param name="list">The URI of the list.</param>
    /// <param name="subject">The DID of the actor belonging to the list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> or <paramref name="subject"/> is <see langword="null"/>.</exception>
    [SetsRequiredMembers]
    public ListItem(AtUri list, Did subject) : base()
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(subject);

        List = list;
        Subject = subject;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ListItem"/> record.
    /// </summary>
    /// <param name="list">The URI of the list.</param>
    /// <param name="subject">The DID of the actor belonging to the list.</param>
    /// <param name="createdAt">The timestamp when the membership record was created.</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> or <paramref name="subject"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    [SetsRequiredMembers]
    public ListItem(AtUri list, Did subject, DateTimeOffset createdAt) : base(createdAt)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(subject);

        List = list;
        Subject = subject;
    }

    /// <summary>
    /// Gets the URI of the list.
    /// </summary>
    /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public required AtUri List
    {
        get
        {
            return _list;
        }

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            _list = value;
        }
    }

    /// <summary>
    /// Gets the DID of the actor belonging to the list.
    /// </summary>
    /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public required Did Subject
    {
        get
        {
            return _subject;
        }

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            _subject = value;
        }
    }
}