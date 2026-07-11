// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

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
        Text = text;

        if (facets is not null)
        {
            Facets = new ReadOnlyCollection<Facet>(facets.ToList().AsReadOnly());
        }
        else
        {
            Facets = null;
        }

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
    /// Gets or sets the rich text <see cref="Facet"/>s of the message, if any.
    /// </summary>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<Facet>? Facets { get; set; }

    /// <summary>
    /// Gets or sets the embedded record of the message, if any.
    /// </summary>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EmbeddedBase? Embed { get; set; }

    /// <summary>
    /// Gets or sets the message this message is replying to. The referenced message must be in the same conversation.
    /// </summary>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReplyReference? ReplyTo { get; set; }
}