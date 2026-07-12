// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Embed;

/// <summary>
/// Interface for generating embedded cards from page metadata.
/// </summary>
public interface IEmbeddedCardGenerator
{
    /// <summary>
    /// Generates an <see cref="EmbeddedExternal"/> record for <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The URI to generate the card from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="EmbeddedExternal"/> if OpenGraph data is found; otherwise, <see langword="null"/>.</returns>
    Task<EmbeddedExternal?> Generate(Uri uri, CancellationToken cancellationToken = default);
}