// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Embed;
using idunno.Bluesky.RichText;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents a message in a chat conversation.
/// </summary>
public sealed record MessageInput
{
    /// <summary>
    /// Creates a new instance of <see cref="MessageInput"/>.
    /// </summary>
    /// <param name="text">The text for the message.</param>
    /// <param name="facets">The rich text <see cref="Facet"/>s for the message, if any.</param>
    /// <param name="embed">The <see cref="EmbeddedRecord"/> for the message, if any.</param>
    /// <param name="replyTo">The message this message is replying to. The referenced message must be in the same conversation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/> exceeds the maximum allowed length.</exception>
    public MessageInput(string text, ICollection<Facet>? facets = null, EmbeddedRecord? embed = null, ReplyReference? replyTo = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.MessageLengthInCharacters);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.MessageLengthInGraphemes);
        Text = text;

        Facets = facets is not null ? [.. facets] : null;

        Embed = embed;
        ReplyTo = replyTo;
    }

    /// <summary>
    /// Gets the text of the message
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string Text { get; init; }

    /// <summary>
    /// Gets the rich text <see cref="Facet"/>s of the message, if any.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A facet's range is a pair of byte offsets into <see cref="Text"/>, so facets are only meaningful alongside the text they were
    /// extracted from. The value supplied is copied, so later changes to the collection passed in are not reflected here.
    /// </para>
    /// </remarks>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<Facet>? Facets
    {
        get => _facets;
        init => _facets = value is not null ? new ReadOnlyCollection<Facet>([.. value]) : null;
    }

    /// <summary>
    /// Gets the embedded record of the message, if any.
    /// </summary>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EmbeddedBase? Embed { get; init; }

    /// <summary>
    /// Gets the message this message is replying to. The referenced message must be in the same conversation.
    /// </summary>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReplyReference? ReplyTo { get; init; }

    private readonly IReadOnlyCollection<Facet>? _facets;
}