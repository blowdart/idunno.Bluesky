// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Preserves a space policy whose Lexicon type is not modeled by the SDK.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
[JsonConverter(typeof(UnknownSpacePolicyConverter))]
public sealed class UnknownSpacePolicy : SpacePolicy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownSpacePolicy"/> class.
    /// </summary>
    /// <param name="value">The policy object, including its string <c>$type</c> discriminator.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not an object with a nonempty string discriminator.</exception>
    public UnknownSpacePolicy(JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.Object ||
            !value.TryGetProperty("$type", out JsonElement discriminator) ||
            discriminator.ValueKind is not JsonValueKind.String ||
            string.IsNullOrWhiteSpace(discriminator.GetString()))
        {
            throw new ArgumentException("The policy must be an object with a nonempty string $type.", nameof(value));
        }

        Value = value.Clone();
    }

    /// <summary>
    /// Gets the complete policy object, including its discriminator.
    /// </summary>
    public JsonElement Value { get; }
}
