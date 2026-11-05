// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Shares atomic event, label and report transactions between the optional adapter and packaged providers.</summary>
internal interface ILabelOzoneStore
{
    /// <summary>Pins the first successful independently observed record reference without changing native case scope.</summary>
    Task<LabelServiceResult<LabelModerationCase>> ResolveRecordCaseAsync(
        LabelAdministrationCaller caller,
        long caseId,
        LabelRecordResolution resolution,
        CancellationToken cancellationToken);

    /// <summary>Reads both subject and reporter deadlines using database time under live inspection authority.</summary>
    Task<LabelServiceResult<LabelMuteState>> ReadMuteAsync(
        LabelAdministrationCaller caller,
        string subject,
        CancellationToken cancellationToken);

    /// <summary>Lists active shared mutes with immutable actor provenance and a database-clock cutoff.</summary>
    Task<LabelServiceResult<IReadOnlyList<LabelMuteEntry>>> ReadMutesAsync(
        LabelAdministrationCaller caller,
        long afterEventId,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Reads retained muted submissions without fabricating a case or promoting historical evidence.</summary>
    Task<LabelServiceResult<IReadOnlyList<LabelPendingModerationReport>>> ReadMutedReportsAsync(
        LabelAdministrationCaller caller,
        long afterId,
        int limit,
        long? reportId,
        CancellationToken cancellationToken);

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
    string? ReportNote,
    LabelMuteChange? Mute = null,
    bool IncludeAssignedReports = false,
    bool EscalateSubject = false,
    bool PreserveEscalatedReview = false);

/// <summary>Distinguishes queue suppression from suppression of a reporting account's review-state changes.</summary>
internal enum LabelMuteKind
{
    Subject,
    Reporter
}

/// <summary>Retains an explicit mute or unmute without accepting an application-clock deadline.</summary>
internal sealed record LabelMuteChange(LabelMuteKind Kind, int? DurationInHours, long? ExpectedEventId = null)
{
    /// <summary>Rejects permanent or nonstandard durations rather than silently rounding them.</summary>
    internal void Validate()
    {
        if (!Enum.IsDefined(Kind) || ExpectedEventId is < 0 ||
            DurationInHours is { } hours && hours is not (6 or 12 or 24 or 48 or 72 or 168 or 720 or 2160))
        {
            throw new ArgumentOutOfRangeException(nameof(DurationInHours), "Choose a supported temporary mute duration.");
        }
    }
}

/// <summary>Projects active mute deadlines and the same database clock used to evaluate them.</summary>
internal sealed record LabelMuteState(DateTimeOffset? MuteUntil, DateTimeOffset? MuteReportingUntil, DateTimeOffset DatabaseTime);

/// <summary>Retains the shared active deadline and the event that established it, without private authentication data.</summary>
internal sealed record LabelMuteEntry(
    long EventId,
    string Subject,
    LabelMuteKind Kind,
    DateTimeOffset Until,
    string Actor,
    DateTimeOffset CreatedAt);

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
