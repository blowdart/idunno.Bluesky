// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Shares atomic event, label and report transactions between the optional adapter and packaged providers.</summary>
internal interface ILabelOzoneStore
{
    /// <summary>Commits a complete event after rechecking authority, policy and every prepared label under the issuer lock.</summary>
    Task<LabelServiceResult<LabelOzoneEvent>> CommitOzoneEventAsync(
        LabelAdministrationCaller caller,
        LabelOzoneEventRequest request,
        long policyRevision,
        IReadOnlyList<LabelDecisionCommit> labels,
        CancellationToken cancellationToken);

    /// <summary>Reads bounded durable events under live inspection authority.</summary>
    Task<LabelServiceResult<IReadOnlyList<LabelOzoneEvent>>> ReadOzoneEventsAsync(
        LabelAdministrationCaller caller,
        long afterId,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Reads an exact operation before preparation, distinguishing absence from a committed external identifier.</summary>
    Task<LabelServiceResult<IReadOnlyList<LabelOzoneEvent>>> ReadOzoneOperationAsync(
        LabelAdministrationCaller caller,
        string operationId,
        CancellationToken cancellationToken);
}

/// <summary>Retains independently validated wire intent without substituting display CIDs into report evidence.</summary>
internal sealed record LabelOzoneEventRequest(
    string OperationId,
    string? ExternalId,
    LabelReportSubject Subject,
    string EventJson,
    string? ModToolJson,
    string Reason,
    LabelCaseLabelAction[] Actions,
    long[] ReportIds,
    string[] ReportTypes,
    bool AllReports,
    bool ResolveSubject,
    bool Automated,
    string? ReportNote);

/// <summary>Retains an event's immutable identity and exact subject alongside the report IDs changed in its transaction.</summary>
internal sealed record LabelOzoneEvent(
    long Id,
    string OperationId,
    string Subject,
    string? Cid,
    string Actor,
    DateTimeOffset CreatedAt,
    string EventJson,
    string? ModToolJson,
    long[] ReportIds,
    bool Automated,
    string? ReportNote);
