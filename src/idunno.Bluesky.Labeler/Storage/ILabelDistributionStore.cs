// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>Reads the published CID-scoped label slots used by protocol distribution.</summary>
/// <remarks><para>Unlike raw history reads, only the latest published sequence for each source, URI,
/// CID and value is retained at the requested snapshot. Negative and expired winners remain visible.</para></remarks>
public interface ILabelDistributionStore
{
    /// <summary>Reads a bounded portion of a fixed published distribution snapshot.</summary>
    /// <param name="streamId">The persistent stream identity.</param>
    /// <param name="after">The exclusive sequence position.</param>
    /// <param name="through">The inclusive published snapshot boundary.</param>
    /// <param name="uriPatterns">The exact or trailing-asterisk subject patterns; an empty list matches all subjects.</param>
    /// <param name="sources">The source filters; an empty list matches every source.</param>
    /// <param name="limit">The maximum returned labels, from 1 through 250.</param>
    /// <param name="scanLimit">The maximum immutable candidate rows inspected, including superseded rows,
    /// from <paramref name="limit"/> through 1,000.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page whose progress includes inspected rows that did not match.</returns>
    /// <exception cref="InvalidDataException">The stream or retained sequence boundaries are invalid.</exception>
    /// <exception cref="ArgumentException">A pattern is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A limit is outside its supported range, or <paramref name="after"/>
    /// is below the current replay floor. A pruned position uses <c>after</c> as the exception parameter name.</exception>
    Task<LabelDistributionPage> ReadDistributionAsync(
        Guid streamId,
        long after,
        long through,
        IReadOnlyList<string> uriPatterns,
        IReadOnlyList<Did> sources,
        int limit = 100,
        int scanLimit = 1000,
        CancellationToken cancellationToken = default);
}
