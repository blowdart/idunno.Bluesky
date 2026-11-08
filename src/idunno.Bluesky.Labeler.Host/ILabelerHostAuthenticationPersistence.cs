// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Host;

/// <summary>Initializes the external database backing canonical website authentication and Data Protection.</summary>
public interface ILabelerHostAuthenticationPersistence
{
    /// <summary>Initializes or validates durable identity, correlation and append-only key storage before serving.</summary>
    /// <param name="cancellationToken">The cancellation token for initialization.</param>
    /// <returns>A task representing initialization.</returns>
    Task InitializeAsync(CancellationToken cancellationToken);
}
