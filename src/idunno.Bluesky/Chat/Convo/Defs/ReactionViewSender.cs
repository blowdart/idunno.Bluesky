// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates information about the sender of a reaction.
/// </summary>
public record ReactionViewSender
{
    /// <summary>
    /// Creates a new instance of <see cref="ReactionViewSender"/>.
    /// </summary>
    /// <param name="did">The <see cref="AtProto.Did"/> of the sender of the reaction.</param>
    [JsonConstructor]
    public ReactionViewSender(Did did)
    {
        Did = did;
    }

    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> of the sender of the reaction.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public Did Did { get; init; }
}