// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Chat.Embed;

/// <summary>
/// Encapsulates a join link embedded in a group chat.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(JoinLink), typeDiscriminator: "chat.bsky.embed.joinLink")]
public record JoinLink : EmbeddedBase
{
    /// <summary>
    /// Creates a new instance of the <see cref="JoinLink"/> class.
    /// </summary>
    /// <param name="code">The join link code.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public JoinLink(string code) : base()
    {
        ArgumentNullException.ThrowIfNull(code);
        Code = code;
    }

    /// <summary>
    /// Gets the join link code.
    /// </summary>
    [JsonRequired]
    public string Code { get; init; }
}