// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Labeler;

internal sealed partial class OzoneModerationAdapter
{
    /// <summary>Validates subject-status filters before reading any potentially empty provider page.</summary>
    private static StatusFilter ReadStatusFilters(HttpContext context)
    {
        ValidateQuery(
            context,
            ["subject", "subjectType", "collections", "includeAllUserRecords", "reviewState",
            "lastReviewedBy", "appealed", "limit", "cursor", "sortDirection", "sortField", "includeMuted", "onlyMuted"]);
        LabelReportSubject? subject = null;
        if (context.Request.Query.ContainsKey("subject"))
        {
            string value = Query(context, "subject");
            subject = value.StartsWith("at://", StringComparison.Ordinal)
                ? LabelReportSubject.Record(new AtUri(value))
                : LabelReportSubject.Account(new Did(value));
        }
        string? subjectType = context.Request.Query.ContainsKey("subjectType") ? Query(context, "subjectType") : null;
        if (subjectType is not (null or "account" or "record" or "conversation"))
        {
            throw new JsonException("The subject type is invalid.");
        }
        if (subjectType == "conversation")
        {
            throw new OzoneOperationException("NotImplemented", "Conversation moderation statuses are not supported.");
        }
        ImmutableArray<string>.Builder collections = ImmutableArray.CreateBuilder<string>();
        foreach (string? collection in context.Request.Query["collections"])
        {
            if (collection is null || !Nsid.TryParse(collection, out _))
            {
                throw new JsonException("A collection must be a valid NSID.");
            }
            collections.Add(collection);
        }
        string? reviewState = context.Request.Query.ContainsKey("reviewState") ? Query(context, "reviewState") : null;
        if (reviewState is not (null or "tools.ozone.moderation.defs#reviewOpen" or "tools.ozone.moderation.defs#reviewClosed" or
            "tools.ozone.moderation.defs#reviewEscalated" or "tools.ozone.moderation.defs#reviewNone"))
        {
            throw new JsonException("The review state is invalid.");
        }
        Did? reviewer = context.Request.Query.ContainsKey("lastReviewedBy") ? new Did(Query(context, "lastReviewedBy")) : null;
        return new(
            Subject: subject,
            SubjectType: subjectType,
            Collections: collections.ToImmutable(),
            IncludeAllUserRecords: StatusBoolean(context, "includeAllUserRecords") ?? false,
            RequestedReviewState: reviewState,
            LastReviewedBy: reviewer,
            Appealed: StatusBoolean(context, "appealed"));
    }

    /// <summary>Reads strict XRPC Boolean values without accepting case variants, numbers or whitespace.</summary>
    private static bool? StatusBoolean(HttpContext context, string name) =>
        !context.Request.Query.ContainsKey(name) ? null : Query(context, name) switch
        {
            "true" => true,
            "false" => false,
            _ => throw new JsonException("A Boolean query parameter must be true or false.")
        };

    /// <summary>Applies Ozone's subject precedence without coalescing distinct retained record versions.</summary>
    private static bool StatusSubjectMatches(StatusFilter filter, LabelModerationCase moderationCase)
    {
        LabelReportSubject subject = moderationCase.Subject;
        if (filter.Subject is { } requested)
        {
            if (subject.Repository != requested.Repository ||
                !filter.IncludeAllUserRecords && subject.Subject != requested.Subject)
            {
                return false;
            }
        }
        else if (filter.SubjectType is { } subjectType && subject.SubjectType != subjectType)
        {
            return false;
        }
        return (filter.SubjectType == "account" || filter.Collections.IsEmpty ||
            subject.RecordUri is { } uri && filter.Collections.Contains(uri.Collection!.ToString(), StringComparer.Ordinal)) &&
            (filter.RequestedReviewState is null || filter.RequestedReviewState == ReviewState(moderationCase));
    }

    /// <summary>Reads the latest actual review and current unresolved appeal under the caller's live authority.</summary>
    /// <remarks><para>Report intake, assignment, reopening and appeal intake are not reviews. Native explicit reviews,
    /// decisions, resolutions and notes are reviews; persisted Ozone events follow their wire event's review semantics.
    /// A supplied review contains the caller's authoritative merge with independently persisted mute events.
    /// Appeal filtering follows the lexicon's unresolved-appeal meaning rather than membership in historical appeals.</para></remarks>
    private async Task<StatusMatch> StatusMatchesAsync(
        StatusFilter filter,
        LabelModerationCase moderationCase,
        LabelAdministrationCaller caller,
        ILabelModerationStore cases,
        CancellationToken token,
        StatusReview? resolvedReview = null)
    {
        if (!StatusSubjectMatches(filter, moderationCase))
        {
            return new(false, null, null, false);
        }
        StatusReview review = resolvedReview ?? await ReadStatusReviewAsync(
            moderationCase: moderationCase,
            caller: caller,
            cases: cases,
            token: token).ConfigureAwait(false);
        if (filter.LastReviewedBy is { } reviewer && reviewer != review.Actor)
        {
            return new(false, review.CreatedAt, review.Actor, false);
        }
        bool appealed = false;
        if (store is ILabelAppealStore appeals)
        {
            IReadOnlyList<LabelAppeal> pending = Require(await appeals.GetAppealsAsync(
                caller: caller,
                caseId: moderationCase.Id,
                status: LabelAppealStatus.Pending,
                limit: 1,
                cancellationToken: token).ConfigureAwait(false));
            appealed = pending.Count != 0;
        }
        else if (filter.Appealed is not null)
        {
            throw new OzoneOperationException("NotImplemented", "Current appeal status is unavailable.");
        }
        return new(filter.Appealed is null || filter.Appealed == appealed, review.CreatedAt, review.Actor, appealed);
    }

    /// <summary>Reads review provenance in durable history sequence order, including unchanged-snapshot mute notes.</summary>
    /// <remarks><para>History sequence, not timestamp, orders reviews that share a database-clock instant.
    /// Mutes address the account or record URI independently of retained record versions; other events
    /// must match both the submitted subject and its CID.</para></remarks>
    private async Task<StatusReview> ReadStatusReviewAsync(
        LabelModerationCase moderationCase,
        LabelAdministrationCaller caller,
        ILabelModerationStore cases,
        CancellationToken token)
    {
        StatusReview review = new(null, null);
        long after = 0;
        while (true)
        {
            IReadOnlyList<LabelCaseReceipt> history = Require(await cases.GetCaseHistoryAsync(
                caller: caller,
                caseId: moderationCase.Id,
                afterSequence: after,
                limit: 100,
                cancellationToken: token).ConfigureAwait(false));
            foreach (LabelCaseReceipt receipt in history)
            {
                bool reviewed = receipt.Action is LabelCaseAction.StartReview or LabelCaseAction.Note or
                    LabelCaseAction.Resolve or LabelCaseAction.Decide;
                if (receipt.IsOzoneEvent)
                {
                    reviewed = false;
                    if (store is not ILabelOzoneStore events)
                    {
                        throw new OzoneOperationException("NotImplemented", "Ozone review provenance is unavailable.");
                    }
                    IReadOnlyList<LabelOzoneEvent> committed = Require(await events.ReadOzoneOperationAsync(
                        caller: caller,
                        operationId: receipt.OperationId,
                        cancellationToken: token).ConfigureAwait(false));
                    foreach (LabelOzoneEvent entry in committed)
                    {
                        if (entry.Subject != moderationCase.Subject.Subject)
                        {
                            continue;
                        }
                        using JsonDocument document = JsonDocument.Parse(entry.EventJson);
                        string? eventType = document.RootElement.GetProperty("$type").GetString();
                        bool mute = eventType is
                            "tools.ozone.moderation.defs#modEventMute" or
                            "tools.ozone.moderation.defs#modEventUnmute" or
                            "tools.ozone.moderation.defs#modEventMuteReporter" or
                            "tools.ozone.moderation.defs#modEventUnmuteReporter";
                        reviewed |= mute || entry.Cid == moderationCase.Subject.Cid?.Value &&
                            eventType is ("tools.ozone.moderation.defs#modEventAcknowledge" or
                            "tools.ozone.moderation.defs#modEventComment");
                    }
                }
                if (reviewed)
                {
                    review = new(receipt.CreatedAt, receipt.Actor);
                }
            }
            if (history.Count < 100)
            {
                break;
            }
            after = history[^1].Sequence;
        }
        return review;
    }

    /// <summary>Retains an immutable, eagerly validated subject-status query.</summary>
    private sealed record StatusFilter(
        LabelReportSubject? Subject,
        string? SubjectType,
        ImmutableArray<string> Collections,
        bool IncludeAllUserRecords,
        string? RequestedReviewState,
        Did? LastReviewedBy,
        bool? Appealed);

    /// <summary>Retains exact-case selection and the same latest-review metadata used to filter and sort it.</summary>
    private sealed record StatusMatch(bool Matches, DateTimeOffset? LastReviewedAt, Did? LastReviewedBy, bool Appealed);

    /// <summary>Retains the time and actor of one authoritative latest review, including an explicit absence.</summary>
    private sealed record StatusReview(DateTimeOffset? CreatedAt, Did? Actor);
}
