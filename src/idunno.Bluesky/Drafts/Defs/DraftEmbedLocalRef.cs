// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Drafts;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a local reference to a file to be embedded in a draft post.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(DraftEmbedLocalRef), typeDiscriminator: "app.bsky.draft.defs#draftEmbedLocalRef")]
public record DraftEmbedLocalRef
{
    /// <summary>
    /// Creates a new instance of <see cref="DraftEmbedLocalRef"/> with the specified local path.
    /// </summary>
    /// <param name="path">The local path to the file to be embedded.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>/</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/>is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the UTF-8 byte length of <paramref name="path"/> is greater than <see cref="Maximum.DraftEmbedLocalRefPathLengthInBytes"/>
    /// or less than <see cref="Maximum.DraftEmbedLocalRefPathMinimumLengthInBytes"/>.
    /// </exception>
    [JsonConstructor]
    public DraftEmbedLocalRef(string path)
    {
        Path = path;
    }

    /// <summary>
    /// Gets the local, on-device ref to file to be embedded. Embeds are currently device-bound for drafts.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when setting to <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when setting to an empty or whitespace value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the UTF-8 byte length of the value is greater than <see cref="Maximum.DraftEmbedLocalRefPathLengthInBytes"/>
    /// or less than <see cref="Maximum.DraftEmbedLocalRefPathMinimumLengthInBytes"/>.
    /// </exception>
    [JsonRequired]
    public string Path
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty or whitespace.", nameof(value));
            }

            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                value.GetUtf8Length(),
                Maximum.DraftEmbedLocalRefPathLengthInBytes);

            ArgumentOutOfRangeException.ThrowIfLessThan(
                value.GetUtf8Length(),
                Maximum.DraftEmbedLocalRefPathMinimumLengthInBytes);

            field = value;
        }
    }
}