// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Provides live-authorized inspection of immutable challenge provenance and explicit review outcomes.</summary>
public interface ILabelAppealStore
{
    /// <summary>Reads a retained appeal after checking live team inspection authority.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="appealId">The positive appeal identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The appeal snapshot or a classified failure.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="appealId"/> is not positive.</exception>
    Task<LabelServiceResult<LabelAppeal>> GetAppealAsync(
        LabelAdministrationCaller caller,
        long appealId,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a bounded live page of appeals without extending successful-read audit.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="caseId">The optional exact review case.</param>
    /// <param name="status">The optional review status.</param>
    /// <param name="afterId">The exclusive appeal identifier.</param>
    /// <param name="limit">The maximum results, between one and one hundred.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorized page or a classified failure.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A filter, identifier or limit is invalid.</exception>
    Task<LabelServiceResult<IReadOnlyList<LabelAppeal>>> GetAppealsAsync(
        LabelAdministrationCaller caller,
        long? caseId = null,
        LabelAppealStatus? status = null,
        long afterId = 0,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Reads every distinct report linked to an appeal through bounded keyset pages.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="appealId">The positive appeal identifier.</param>
    /// <param name="afterReportId">The exclusive report identifier.</param>
    /// <param name="limit">The maximum results, between one and one hundred.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The protected report identifiers or a classified failure.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An identifier or limit is invalid.</exception>
    Task<LabelServiceResult<IReadOnlyList<long>>> GetAppealReportsAsync(
        LabelAdministrationCaller caller,
        long appealId,
        long afterReportId = 0,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
