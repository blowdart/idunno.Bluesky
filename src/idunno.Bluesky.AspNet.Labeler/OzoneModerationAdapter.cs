// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Translates the supported Ozone wire operations without authenticating request-body identities.</summary>
internal sealed partial class OzoneModerationAdapter(
    ILabelStore store,
    ILabelAdministrationStore administration,
    ILabelIssuancePolicyProvider policy,
    ILabelSigningKeyRing? keys,
    HttpClient identity,
    TimeProvider clock,
    LabelerHost? host)
{
    /// <summary>Dispatches one authenticated, method-bound request with explicit unsupported-feature errors.</summary>
    internal async Task<(AdministrationResponse Response, byte[]? Payload)> DispatchAsync(
        HttpContext context,
        string method,
        LabelAdministrationCaller caller)
    {
        if (method.StartsWith("tools.ozone.team.", StringComparison.Ordinal))
        {
            return (new(false, "NotImplemented", "Ozone team mutations are not supported."), null);
        }
        if (policy is not LabelPolicyService || store is not ILabelModerationStore cases ||
            store is not ILabelReportStore reports || store is not ILabelOzoneStore events ||
            store is not ILabelPolicyStore policies)
        {
            return (new(false, "ModerationDisabled", "Enable shared labeler moderation before using Ozone moderation."), null);
        }
        try
        {
            CancellationToken token = context.RequestAborted;
            byte[] payload = method switch
            {
                "tools.ozone.report.queryReports" => await QueryReportsAsync(
                    context: context,
                    caller: caller,
                    reports: reports,
                    cases: cases,
                    events: events,
                    token: token).ConfigureAwait(false),
                "tools.ozone.report.getReport" => await GetReportAsync(
                    context: context,
                    caller: caller,
                    reports: reports,
                    cases: cases,
                    events: events,
                    token: token).ConfigureAwait(false),
                "tools.ozone.report.listActivities" => await ListActivitiesAsync(
                    context: context,
                    caller: caller,
                    reports: reports,
                    events: events,
                    token: token).ConfigureAwait(false),
                "tools.ozone.moderation.queryStatuses" => await QueryStatusesAsync(
                    context: context,
                    caller: caller,
                    reports: reports,
                    cases: cases,
                    events: events,
                    token: token).ConfigureAwait(false),
                "tools.ozone.moderation.queryEvents" => await QueryEventsAsync(
                    context: context,
                    caller: caller,
                    reports: reports,
                    cases: cases,
                    events: events,
                    token: token).ConfigureAwait(false),
                "tools.ozone.moderation.getEvent" => await GetEventAsync(
                    context: context,
                    caller: caller,
                    reports: reports,
                    cases: cases,
                    events: events,
                    token: token).ConfigureAwait(false),
                "tools.ozone.moderation.getRepo" => await GetRepoAsync(
                    context: context,
                    caller: caller,
                    cases: cases,
                    token: token).ConfigureAwait(false),
                "tools.ozone.moderation.getRecord" => await GetRecordAsync(
                    context: context,
                    caller: caller,
                    cases: cases,
                    token: token).ConfigureAwait(false),
                "tools.ozone.moderation.getRepos" => await GetSubjectsAsync(
                    context: context,
                    caller: caller,
                    cases: cases,
                    records: false,
                    token: token).ConfigureAwait(false),
                "tools.ozone.moderation.getRecords" => await GetSubjectsAsync(
                    context: context,
                    caller: caller,
                    cases: cases,
                    records: true,
                    token: token).ConfigureAwait(false),
                "app.bsky.actor.getProfile" => await GetProfileAsync(context, token).ConfigureAwait(false),
                "app.bsky.feed.getPostThread" => await GetPostThreadAsync(context, token).ConfigureAwait(false),
                "tools.ozone.setting.listOptions" => await ListOptionsAsync(context, caller, token).ConfigureAwait(false),
                "tools.ozone.setting.upsertOption" => await UpsertMediaOptionsAsync(context, caller, token).ConfigureAwait(false),
                "tools.ozone.moderation.emitEvent" or "tools.ozone.report.closeReports" or "tools.ozone.report.createActivity" =>
                    await EmitAsync(
                        context: context,
                        method: method,
                        caller: caller,
                        reports: reports,
                        events: events,
                        policies: policies,
                        token: token).ConfigureAwait(false),
                _ => throw new OzoneOperationException("NotImplemented", "This Ozone operation is not supported by the labeler.")
            };
            return (new(true), payload);
        }
        catch (OzoneOperationException exception)
        {
            return (new(false, exception.Code, exception.Message), null);
        }
    }

    /// <summary>Reads only retained report evidence; optional enrichment never changes its subject or CID.</summary>
    private static async Task<byte[]> GetReportAsync(
        HttpContext context,
        LabelAdministrationCaller caller,
        ILabelReportStore reports,
        ILabelModerationStore cases,
        ILabelOzoneStore events,
        CancellationToken token)
    {
        ValidateQuery(context, ["id"]);
        LabelServiceResult<LabelModerationReport> result = await reports.GetReportAsync(
            caller,
            Number(context, "id"),
            token).ConfigureAwait(false);
        if (!result.Succeeded && result.Error.Code == LabelServiceErrorCode.OperationNotFound)
        {
            IReadOnlyList<LabelPendingModerationReport> retained = Require(await events.ReadMutedReportsAsync(
                caller: caller,
                afterId: 0,
                limit: 1,
                reportId: Number(context, "id"),
                cancellationToken: token).ConfigureAwait(false));
            if (retained.Count != 0)
            {
                return Encode(writer => WriteMutedReport(writer, retained[0]));
            }
            throw new OzoneOperationException("NotFound", result.Error.Message);
        }
        LabelModerationReport report = Require(result);
        LabelModerationCase moderationCase = Require(await cases.GetCaseAsync(caller, report.CaseId, token).ConfigureAwait(false));
        return Encode(writer => WriteReport(writer, report, moderationCase));
    }

    /// <summary>Lists report activities actually committed by this adapter, retaining their moderator provenance.</summary>
    private static async Task<byte[]> ListActivitiesAsync(
        HttpContext context,
        LabelAdministrationCaller caller,
        ILabelReportStore reports,
        ILabelOzoneStore events,
        CancellationToken token)
    {
        ValidateQuery(context, ["reportId", "limit", "cursor"]);
        long reportId = Number(context, "reportId");
        LabelServiceResult<LabelModerationReport> report = await reports.GetReportAsync(caller, reportId, token).ConfigureAwait(false);
        if (!report.Succeeded && report.Error.Code == LabelServiceErrorCode.OperationNotFound)
        {
            IReadOnlyList<LabelPendingModerationReport> retained = Require(await events.ReadMutedReportsAsync(
                caller: caller,
                afterId: 0,
                limit: 1,
                reportId: reportId,
                cancellationToken: token).ConfigureAwait(false));
            if (retained.Count == 0)
            {
                Require(report);
            }
        }
        else
        {
            Require(report);
        }
        List<OzonePageItem> selected = [];
        long after = 0;
        while (true)
        {
            IReadOnlyList<LabelOzoneEvent> page = Require(await events.ReadOzoneEventsAsync(
                caller: caller,
                afterId: after,
                limit: 100,
                cancellationToken: token).ConfigureAwait(false));
            foreach (LabelOzoneEvent entry in page)
            {
                int index = Array.IndexOf(entry.ReportIds, reportId);
                if (index < 0)
                {
                    continue;
                }
                long id = checked(entry.Id * 1024 + index);
                Consider(
                    context: context,
                    caller: caller,
                    items: selected,
                    item: new(
                        id,
                        entry.CreatedAt,
                        Encode(writer =>
                    {
                        writer.WriteStartObject();
                        writer.WriteNumber("id", id);
                        writer.WriteNumber("reportId", reportId);
                        writer.WriteStartObject("activity");
                        writer.WriteString("$type", "tools.ozone.report.defs#closeActivity");
                        writer.WriteEndObject();
                        writer.WriteBoolean("isAutomated", entry.Automated);
                        writer.WriteString("createdBy", entry.Actor);
                        writer.WriteString("createdAt", entry.CreatedAt);
                        if (entry.ReportNote is { } note)
                        {
                            writer.WriteString("internalNote", note);
                        }
                        writer.WriteEndObject();
                    })));
            }
            if (page.Count < 100)
            {
                break;
            }
            after = page[^1].Id;
        }
        return Page(
            context: context,
            caller: caller,
            name: "activities",
            items: selected);
    }

    /// <summary>Filters live report pages with bounded memory and a filter-bound chronological continuation.</summary>
    private static async Task<byte[]> QueryReportsAsync(
        HttpContext context,
        LabelAdministrationCaller caller,
        ILabelReportStore reports,
        ILabelModerationStore cases,
        ILabelOzoneStore events,
        CancellationToken token)
    {
        ValidateQuery(
            context,
            ["subject", "did", "subjectType", "status", "reportTypes", "collections", "assignedTo",
            "isMuted", "queueId", "limit", "cursor", "sortField", "sortDirection"]);
        string status = RequiredQuery(context, "status");
        bool? isMuted = context.Request.Query.ContainsKey("isMuted") ? bool.Parse(Query(context, "isMuted")) : null;
        if (status is not ("open" or "closed" or "assigned" or "queued" or "escalated"))
        {
            throw new JsonException("The report status is invalid.");
        }
        string sort = Query(context, "sortField", "createdAt");
        if (sort is not ("createdAt" or "updatedAt"))
        {
            throw new JsonException("The report sort field is invalid.");
        }
        List<OzonePageItem> selected = [];
        long after = 0;
        while (true)
        {
            IReadOnlyList<LabelModerationReport> page = Require(await reports.GetReportsAsync(
                caller: caller,
                afterId: after,
                limit: 100,
                cancellationToken: token).ConfigureAwait(false));
            foreach (LabelModerationReport report in page)
            {
                LabelReportSubject subject = report.Submission.Subject;
                LabelModerationCase moderationCase = Require(await cases.GetCaseAsync(caller, report.CaseId, token).ConfigureAwait(false));
                if (!MatchesSubject(context, subject) || ReportStatus(report, moderationCase) != status ||
                    context.Request.Query.ContainsKey("reportTypes") &&
                        !context.Request.Query["reportTypes"].Contains(report.Submission.ReasonType, StringComparer.Ordinal) ||
                    context.Request.Query.ContainsKey("assignedTo") && Query(context, "assignedTo") != moderationCase.Assignee?.Value ||
                    isMuted == true ||
                    context.Request.Query.ContainsKey("queueId") && Number(context, "queueId") != -1)
                {
                    continue;
                }
                Consider(
                    context: context,
                    caller: caller,
                    items: selected,
                    item: new(
                        Id: report.Id,
                        Time: sort == "createdAt" ? report.CreatedAt : report.HandledAt ?? report.CreatedAt,
                        Payload: Encode(writer => WriteReport(writer, report, moderationCase))));
            }
            if (page.Count < 100)
            {
                break;
            }
            after = page[^1].Id;
        }
        if (isMuted != false && status == "open" && !context.Request.Query.ContainsKey("assignedTo") &&
            (!context.Request.Query.ContainsKey("queueId") || Number(context, "queueId") == -1))
        {
            after = 0;
            while (true)
            {
                IReadOnlyList<LabelPendingModerationReport> page = Require(await events.ReadMutedReportsAsync(
                    caller: caller, afterId: after, limit: 100, reportId: null, cancellationToken: token).ConfigureAwait(false));
                foreach (LabelPendingModerationReport report in page)
                {
                    if (!MatchesSubject(context, report.Submission.Subject) ||
                        context.Request.Query.ContainsKey("reportTypes") &&
                        !context.Request.Query["reportTypes"].Contains(report.Submission.ReasonType, StringComparer.Ordinal))
                    {
                        continue;
                    }
                    Consider(
                        context: context,
                        caller: caller,
                        items: selected,
                        item: new(
                            Id: report.Id,
                            Time: report.CreatedAt,
                            Payload: Encode(writer => WriteMutedReport(writer, report))));
                }
                if (page.Count < 100)
                {
                    break;
                }
                after = page[^1].Id;
            }
        }
        return Page(
            context: context,
            caller: caller,
            name: "reports",
            items: selected);
    }

    /// <summary>Projects native case workflows while keeping distinct CID-scoped cases intact.</summary>
    private async Task<byte[]> QueryStatusesAsync(
        HttpContext context,
        LabelAdministrationCaller caller,
        ILabelReportStore reports,
        ILabelModerationStore cases,
        ILabelOzoneStore events,
        CancellationToken token)
    {
        ValidateQuery(context,
            ["subject", "reviewState", "limit", "cursor", "sortDirection", "sortField", "includeMuted", "onlyMuted",
            "subjectType", "collections", "includeAllUserRecords", "lastReviewedBy", "appealed"]);
        StatusFilter filters = ReadStatusFilters(context);
        bool includeMuted = context.Request.Query.ContainsKey("includeMuted") && bool.Parse(Query(context, "includeMuted"));
        bool onlyMuted = context.Request.Query.ContainsKey("onlyMuted") && bool.Parse(Query(context, "onlyMuted"));
        string sort = Query(context, "sortField", "lastReportedAt");
        if (sort is not ("lastReportedAt" or "lastReviewedAt"))
        {
            throw new OzoneOperationException("NotImplemented", "Only chronological subject-status sorting is supported.");
        }
        List<OzonePageItem> selected = [];
        long after = 0;
        int unresolved = 0;
        while (true)
        {
            IReadOnlyList<LabelModerationCase> page = Require(await cases.GetCasesAsync(
                caller: caller,
                afterId: after,
                limit: 100,
                cancellationToken: token).ConfigureAwait(false));
            foreach (LabelModerationCase candidate in page)
            {
                LabelModerationCase moderationCase = candidate;
                StatusMatch match = await StatusMatchesAsync(filters, moderationCase, caller, cases, token).ConfigureAwait(false);
                if (!match.Matches ||
                    context.Request.Query.ContainsKey("reviewState") && Query(context, "reviewState") != ReviewState(moderationCase))
                {
                    continue;
                }
                LabelMuteState mute = Require(await events.ReadMuteAsync(caller, moderationCase.Subject.Subject, token).ConfigureAwait(false));
                if (!includeMuted && mute.MuteUntil is not null ||
                    onlyMuted && mute.MuteUntil is null && mute.MuteReportingUntil is null)
                {
                    continue;
                }
                if (moderationCase.Subject.RecordUri is not null && moderationCase.Subject.Cid is null)
                {
                    moderationCase = await ResolveCaseAsync(
                        caller: caller,
                        moderationCase: moderationCase,
                        events: events,
                        token: token,
                        retryRecentlyObserved: context.Request.Query.ContainsKey("subject")).ConfigureAwait(false);
                    if (moderationCase.RecordResolution?.Cid is null)
                    {
                        unresolved++;
                        continue;
                    }
                }
                DateTimeOffset? lastReported = null;
                long afterReport = 0;
                while (true)
                {
                    IReadOnlyList<LabelModerationReport> reportPage = Require(await reports.GetReportsAsync(
                        caller: caller,
                        caseId: moderationCase.Id,
                        afterId: afterReport,
                        limit: 100,
                        cancellationToken: token).ConfigureAwait(false));
                    lastReported = reportPage.Select(report => (DateTimeOffset?)report.CreatedAt).Append(lastReported).Max();
                    if (reportPage.Count < 100)
                    {
                        break;
                    }
                    afterReport = reportPage[^1].Id;
                }
                DateTimeOffset? lastReviewed = match.LastReviewedAt;
                Consider(
                    context: context,
                    caller: caller,
                    items: selected,
                    item: new(
                        moderationCase.Id,
                        (sort == "lastReportedAt" ? lastReported : lastReviewed) ?? DateTimeOffset.UnixEpoch,
                        Encode(writer =>
                    {
                        writer.WriteStartObject();
                        WriteStatusProperties(writer, moderationCase);
                        writer.WriteBoolean("appealed", match.Appealed);
                        if (match.LastReviewedBy is { } reviewer)
                        {
                            writer.WriteString("lastReviewedBy", reviewer);
                        }
                        if (mute.MuteUntil is { } muteUntil)
                        {
                            writer.WriteString("muteUntil", muteUntil);
                        }
                        if (mute.MuteReportingUntil is { } reportingUntil)
                        {
                            writer.WriteString("muteReportingUntil", reportingUntil);
                        }
                        if (lastReported is { } reported)
                        {
                            writer.WriteString("lastReportedAt", reported);
                        }
                        if (lastReviewed is { } reviewed)
                        {
                            writer.WriteString("lastReviewedAt", reviewed);
                        }
                        writer.WriteEndObject();
                    })));
            }
            if (page.Count < 100)
            {
                break;
            }
            after = page[^1].Id;
        }
        return Page(
            context: context,
            caller: caller,
            name: "subjectStatuses",
            items: selected,
            unresolved: unresolved);
    }

    /// <summary>Combines durable report, native case and Ozone event identities without colliding or rewriting history.</summary>
    private async Task<byte[]> QueryEventsAsync(
        HttpContext context,
        LabelAdministrationCaller caller,
        ILabelReportStore reports,
        ILabelModerationStore cases,
        ILabelOzoneStore events,
        CancellationToken token)
    {
        ValidateQuery(
            context,
            ["subject", "types", "createdBy", "createdAfter", "createdBefore", "hasComment",
            "collections", "limit", "cursor", "sortDirection", "includeAllUserRecords", "withStrike"]);
        bool includeAllUserRecords = StatusBoolean(context, "includeAllUserRecords") ?? false;
        bool withStrike = StatusBoolean(context, "withStrike") ?? false;
        string? requestedSubject = context.Request.Query.ContainsKey("subject") ? Query(context, "subject") : null;
        LabelReportSubject? requested = null;
        if (requestedSubject is not null)
        {
            requested = requestedSubject.StartsWith("at://", StringComparison.Ordinal)
                ? LabelReportSubject.Record(new AtUri(requestedSubject))
                : LabelReportSubject.Account(new Did(requestedSubject));
        }
        DateTimeOffset? createdAfter = context.Request.Query.ContainsKey("createdAfter")
            ? DateTimeOffset.Parse(Query(context, "createdAfter"), CultureInfo.InvariantCulture) : null;
        DateTimeOffset? createdBefore = context.Request.Query.ContainsKey("createdBefore")
            ? DateTimeOffset.Parse(Query(context, "createdBefore"), CultureInfo.InvariantCulture) : null;
        bool? hasComment = StatusBoolean(context, "hasComment");
        List<OzonePageItem> selected = [];
        int unresolved = 0;
        await VisitEventsAsync(
            caller: caller,
            reports: reports,
            cases: cases,
            events: events,
            visit: item =>
        {
            using JsonDocument document = JsonDocument.Parse(item.Payload);
            JsonElement root = document.RootElement;
            JsonElement subject = root.GetProperty("subject");
            string target = subject.TryGetProperty("uri", out JsonElement uri) ? uri.GetString()! : subject.GetProperty("did").GetString()!;
            if (requested is not null &&
                    (includeAllUserRecords
                        ? requested.Repository.Value != EventRepository(target)
                        : requested.Subject != target) ||
                withStrike && (!root.GetProperty("event").TryGetProperty("strikeCount", out JsonElement strike) ||
                    strike.ValueKind == JsonValueKind.Null) ||
                context.Request.Query.ContainsKey("types") &&
                    !context.Request.Query["types"].Contains(root.GetProperty("event").GetProperty("$type").GetString(), StringComparer.Ordinal) ||
                context.Request.Query.ContainsKey("createdBy") && Query(context, "createdBy") != root.GetProperty("createdBy").GetString() ||
                createdAfter is { } after && item.Time <= after ||
                createdBefore is { } before && item.Time >= before ||
                hasComment is { } commented && commented !=
                    (root.GetProperty("event").TryGetProperty("comment", out JsonElement comment) && !string.IsNullOrEmpty(comment.GetString())))
            {
                return;
            }
            if (context.Request.Query.ContainsKey("collections") &&
                (!target.StartsWith("at://", StringComparison.Ordinal) ||
                    !context.Request.Query["collections"].Contains(new AtUri(target).Collection!.ToString(), StringComparer.Ordinal)))
            {
                return;
            }
            if (subject.TryGetProperty("did", out JsonElement repository) &&
                repository.GetString()!.StartsWith("at://", StringComparison.Ordinal))
            {
                unresolved++;
                return;
            }
            Consider(
                context: context,
                caller: caller,
                items: selected,
                item: item);
        },
            token: token).ConfigureAwait(false);
        return Page(
            context: context,
            caller: caller,
            name: "events",
            items: selected,
            unresolved: unresolved);
    }

    /// <summary>Reads exact event identity and hydrates only its display subject.</summary>
    private async Task<byte[]> GetEventAsync(
        HttpContext context,
        LabelAdministrationCaller caller,
        ILabelReportStore reports,
        ILabelModerationStore cases,
        ILabelOzoneStore events,
        CancellationToken token)
    {
        ValidateQuery(context, ["id"]);
        long id = Number(context, "id");
        OzonePageItem? found = null;
        await VisitEventsAsync(
            caller: caller,
            reports: reports,
            cases: cases,
            events: events,
            visit: item =>
        {
            if (item.Id == id)
            {
                found = item;
            }
        },
            token: token).ConfigureAwait(false);
        if (found is null)
        {
            throw new OzoneOperationException("EventNotFound", "The moderation event was not found.");
        }
        using JsonDocument document = JsonDocument.Parse(found.Payload);
        JsonElement reference = document.RootElement.GetProperty("subject");
        LabelReportSubject subject = ModerationWire.ParseSubject(reference);
        byte[] detail;
        try
        {
            detail = subject.RecordUri is { } uri
                ? await RecordAsync(
                    caller: caller,
                    uri: uri,
                    cid: subject.Cid,
                    cases: cases,
                    token: token).ConfigureAwait(false)
                : await RepoAsync(
                    caller: caller,
                    did: subject.Repository,
                    cases: cases,
                    token: token).ConfigureAwait(false);
        }
        catch (OzoneOperationException exception) when (exception.Code is "RecordNotFound" or "RepoNotFound")
        {
            detail = Encode(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString(
                    "$type",
                    subject.RecordUri is null
                    ? "tools.ozone.moderation.defs#repoViewNotFound" : "tools.ozone.moderation.defs#recordViewNotFound");
                writer.WriteString(subject.RecordUri is null ? "did" : "uri", subject.Subject);
                writer.WriteEndObject();
            });
        }
        return Encode(writer =>
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in document.RootElement.EnumerateObject().Where(property => property.Name is not ("subject" or "subjectBlobCids")))
            {
                property.WriteTo(writer);
            }
            writer.WritePropertyName("subject");
            using JsonDocument subjectDocument = JsonDocument.Parse(detail);
            writer.WriteStartObject();
            string defaultType = subject.RecordUri is null ? "tools.ozone.moderation.defs#repoView" : "tools.ozone.moderation.defs#recordView";
            writer.WriteString(
                "$type",
                subjectDocument.RootElement.TryGetProperty("$type", out JsonElement detailType)
                ? detailType.GetString() : defaultType);
            foreach (JsonProperty property in subjectDocument.RootElement.EnumerateObject().Where(property => property.Name is not ("$type" or "blobs")))
            {
                property.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.WriteStartArray("subjectBlobs");
            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    /// <summary>Reads actual repository identity, without exposing PDS administration fields.</summary>
    private Task<byte[]> GetRepoAsync(
        HttpContext context,
        LabelAdministrationCaller caller,
        ILabelModerationStore cases,
        CancellationToken token)
    {
        ValidateQuery(context, ["did"]);
        return RepoAsync(
            caller: caller,
            did: new Did(RequiredQuery(context, "did")),
            cases: cases,
            token: token);
    }

    /// <summary>Reads an actual record version for display without changing stored report evidence.</summary>
    private Task<byte[]> GetRecordAsync(
        HttpContext context,
        LabelAdministrationCaller caller,
        ILabelModerationStore cases,
        CancellationToken token)
    {
        ValidateQuery(context, ["uri", "cid"]);
        return RecordAsync(
            caller: caller,
            uri: new AtUri(RequiredQuery(context, "uri")),
            cid: context.Request.Query.ContainsKey("cid") ? new Cid(Query(context, "cid")) : null,
            cases: cases,
            token: token);
    }

    /// <summary>Fetches repository identity and records the local observation time, not a fabricated account-creation time.</summary>
    private async Task<byte[]> RepoAsync(
        LabelAdministrationCaller caller,
        Did did,
        ILabelModerationStore cases,
        CancellationToken token)
    {
        DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
            did: did,
            httpClient: identity,
            cancellationToken: token).ConfigureAwait(false);
        if (document is null || document.Id != did)
        {
            throw new OzoneOperationException("ServiceUnavailable", "A matching repository DID document could not be resolved.");
        }
        Handle? handle = await IdentityResolution.ResolveVerifiedHandleAsync(
            did: did,
            httpClient: identity,
            cancellationToken: token).ConfigureAwait(false);
        if (handle is null)
        {
            throw new OzoneOperationException("ServiceUnavailable", "The repository handle could not be verified.");
        }
        LabelModerationCase? status = await LatestCaseAsync(
            caller: caller,
            subject: LabelReportSubject.Account(did),
            cases: cases,
            token: token).ConfigureAwait(false);
        StoredLabel[] labels = store is ILabelTableStore table
            ? Require(await table.ReadSubjectLabelsAsync(caller, LabelReportSubject.Account(did), token).ConfigureAwait(false))
            : throw new OzoneOperationException("ServiceUnavailable", "Subject label inspection is unavailable.");
        return Encode(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("did", did.Value);
            writer.WriteString("handle", handle.ToString());
            writer.WriteStartArray("relatedRecords");
            writer.WriteEndArray();
            writer.WriteString("indexedAt", clock.GetUtcNow());
            WriteModeration(writer, status);
            WriteLabels(writer, labels);
            writer.WriteEndObject();
        });
    }

    /// <summary>Fetches the requested record CID and value from the independently resolved PDS.</summary>
    private async Task<byte[]> RecordAsync(
        LabelAdministrationCaller caller,
        AtUri uri,
        Cid? cid,
        ILabelModerationStore cases,
        CancellationToken token)
    {
        Did did = uri.Repo as Did ?? throw new JsonException("A record URI requires a DID repository.");
        if (uri.Collection is null || uri.RecordKey is null)
        {
            throw new JsonException("A complete record URI is required.");
        }
        Uri? pds = await IdentityResolution.ResolvePdsAsync(did: did, httpClient: identity, cancellationToken: token).ConfigureAwait(false);
        if (pds is null)
        {
            throw new OzoneOperationException("ServiceUnavailable", "The repository PDS could not be resolved.");
        }
        AtProtoHttpResult<AtProtoRepositoryRecord> result = await AtProtoServer.GetRecord(
            repo: did,
            collection: uri.Collection,
            rKey: uri.RecordKey,
            cid: cid,
            service: pds,
            accessCredentials: null,
            httpClient: identity,
            cancellationToken: token).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new OzoneOperationException(
                result.AtErrorDetail?.Error == "RecordNotFound" ? "RecordNotFound" : "ServiceUnavailable",
                "The requested record could not be retrieved from its PDS.");
        }
        if (result.Result.Uri != uri || cid is not null && result.Result.Cid != cid || result.Result.Value is null)
        {
            throw new OzoneOperationException("ServiceUnavailable", "The PDS returned an inconsistent record identity or value.");
        }
        LabelReportSubject display = LabelReportSubject.Record(uri, result.Result.Cid);
        LabelModerationCase? status = await LatestCaseAsync(
            caller: caller,
            subject: display,
            cases: cases,
            token: token).ConfigureAwait(false);
        StoredLabel[] labels = store is ILabelTableStore table
            ? Require(await table.ReadSubjectLabelsAsync(caller, display, token).ConfigureAwait(false))
            : throw new OzoneOperationException("ServiceUnavailable", "Subject label inspection is unavailable.");
        byte[] repo = await RepoAsync(
            caller: caller,
            did: did,
            cases: cases,
            token: token).ConfigureAwait(false);
        return Encode(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("uri", uri.ToString());
            writer.WriteString("cid", result.Result.Cid.ToString());
            writer.WritePropertyName("value");
            result.Result.Value.WriteTo(writer);
            writer.WriteStartArray("blobs");
            writer.WriteEndArray();
            writer.WriteString("indexedAt", clock.GetUtcNow());
            WriteModeration(writer, status);
            WriteLabels(writer, labels);
            writer.WritePropertyName("repo");
            using JsonDocument repoDocument = JsonDocument.Parse(repo);
            repoDocument.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        });
    }

    /// <summary>Commits a supported label or acknowledgment event with exact reporter selection and no implicit appeal outcome.</summary>
    private async Task<byte[]> EmitAsync(
        HttpContext context,
        string method,
        LabelAdministrationCaller caller,
        ILabelReportStore reports,
        ILabelOzoneStore events,
        ILabelPolicyStore policies,
        CancellationToken token)
    {
        byte[] body = await AdministrationWire.ReadAsync(context.Request.Body, 32768, token).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
        JsonElement root = document.RootElement;
        ValidateJson(root);
        LabelReportSubject subject;
        JsonElement eventValue;
        long[] ids = [];
        string[] types = [];
        bool all = false;
        bool resolve = false;
        bool escalate = false;
        bool automated = false;
        string? external = null;
        string? tool = null;
        string? reportNote = null;
        string reason;
        bool activity = method == "tools.ozone.report.createActivity";
        if (method == "tools.ozone.moderation.emitEvent")
        {
            ValidateObject(root, ["createdBy", "subject", "event", "subjectBlobCids", "modTool", "externalId", "reportAction"]);
            if (new Did(Text(root, "createdBy")) != caller.Actor)
            {
                throw new OzoneOperationException("Unauthorized", "createdBy must match the service-authenticated caller.");
            }
            subject = ModerationWire.ParseSubject(Field(root, "subject"));
            if (subject.RecordUri is not null && subject.Cid is null)
            {
                throw new OzoneOperationException(
                    "NotImplemented",
                    "Ozone record events require a real CID; URI-only report resolution uses the report APIs.");
            }
            eventValue = Field(root, "event");
            reason = OptionalText(eventValue, "comment") ?? "Ozone moderation event.";
            external = OptionalText(root, "externalId");
            if (root.TryGetProperty("modTool", out JsonElement modTool))
            {
                ValidateObject(modTool, ["name", "meta"]);
                _ = Text(modTool, "name");
                tool = modTool.GetRawText();
            }
            if (root.TryGetProperty("subjectBlobCids", out JsonElement blobs) &&
                (blobs.ValueKind != JsonValueKind.Array || blobs.GetArrayLength() != 0))
            {
                throw new OzoneOperationException("NotImplemented", "Blob moderation is not supported.");
            }
            if (root.TryGetProperty("reportAction", out JsonElement reportAction))
            {
                ValidateObject(reportAction, ["ids", "types", "all", "note", "isAutomated"]);
                ids = Numbers(reportAction, "ids");
                types = Strings(reportAction, "types");
                all = Boolean(reportAction, "all");
                automated = Boolean(reportAction, "isAutomated");
                if (ids.Length == 0 && types.Length == 0 && !all)
                {
                    throw new JsonException("reportAction requires ids, types or all.");
                }
                reportNote = OptionalText(reportAction, "note");
            }
        }
        else
        {
            ValidateObject(
                root,
                activity
                ? ["reportId", "eventId", "activity", "internalNote", "publicNote", "isAutomated", "meta"]
                : ["subject", "includeAllOwned", "reportTypes", "excludedReportTypes", "internalNote", "isAutomated", "meta"]);
            if (OptionalText(root, "publicNote") is not null || root.TryGetProperty("meta", out _) ||
                Boolean(root, "includeAllOwned") || Strings(root, "excludedReportTypes").Length > 0)
            {
                throw new OzoneOperationException("NotImplemented", "Public notes, custom activity metadata and account-wide report closure are not supported.");
            }
            reason = OptionalText(root, "internalNote") ?? "Ozone report resolution.";
            reportNote = reason;
            if (activity)
            {
                if (root.TryGetProperty("reportId", out _) == root.TryGetProperty("eventId", out _))
                {
                    throw new JsonException("Provide exactly one reportId or eventId.");
                }
                long id = root.TryGetProperty("reportId", out _) ? Integer(root, "reportId") : Integer(root, "eventId") / 4;
                if (root.TryGetProperty("eventId", out _) && Integer(root, "eventId") % 4 != 0)
                {
                    throw new JsonException("The eventId must identify a report event.");
                }
                LabelModerationReport report = Require(await reports.GetReportAsync(caller, id, token).ConfigureAwait(false));
                subject = report.Submission.Subject;
                eventValue = Field(root, "activity");
                ValidateObject(eventValue, ["$type"]);
                if (Text(eventValue, "$type") != "tools.ozone.report.defs#closeActivity")
                {
                    throw new OzoneOperationException("NotImplemented", "Only close report activities are supported in this milestone.");
                }
                ids = [id];
                resolve = true;
            }
            else
            {
                string target = Text(root, "subject");
                subject = target.StartsWith("at://", StringComparison.Ordinal)
                    ? LabelReportSubject.Record(new AtUri(target)) : LabelReportSubject.Account(new Did(target));
                types = Strings(root, "reportTypes");
                all = types.Length == 0;
                resolve = true;
                using JsonDocument acknowledge = JsonDocument.Parse("""{"$type":"tools.ozone.moderation.defs#modEventAcknowledge"}""");
                eventValue = acknowledge.RootElement.Clone();
            }
        }
        string eventType = Text(eventValue, "$type");
        List<LabelCaseLabelAction> actions = [];
        LabelMuteChange? mute = null;
        if (eventType == "tools.ozone.moderation.defs#modEventLabel")
        {
            ValidateObject(eventValue, ["$type", "createLabelVals", "negateLabelVals", "comment", "durationInHours"]);
            string[] create = Strings(eventValue, "createLabelVals", required: true);
            string[] negate = Strings(eventValue, "negateLabelVals", required: true);
            if (create.Concat(negate).Distinct(StringComparer.Ordinal).Count() != create.Length + negate.Length ||
                create.Length + negate.Length > 16)
            {
                throw new JsonException("Submit at most sixteen distinct label actions.");
            }
            DateTimeOffset? expiry = null;
            if (eventValue.TryGetProperty("durationInHours", out _))
            {
                int duration = checked((int)Integer(eventValue, "durationInHours"));
                ArgumentOutOfRangeException.ThrowIfLessThan(duration, 1);
                expiry = clock.GetUtcNow().AddHours(duration);
            }
            actions.AddRange(create.Select(value => new LabelCaseLabelAction(LabelOperationKind.Issue, value, expiry)));
            actions.AddRange(negate.Select(value => new LabelCaseLabelAction(LabelOperationKind.Negate, value)));
            resolve = actions.Count > 0;
        }
        else if (eventType == "tools.ozone.moderation.defs#modEventAcknowledge")
        {
            ValidateObject(eventValue, ["$type", "comment", "acknowledgeAccountSubjects"]);
            if (Boolean(eventValue, "acknowledgeAccountSubjects"))
            {
                throw new OzoneOperationException("NotImplemented", "Account-wide acknowledgment is not supported.");
            }
            resolve = true;
        }
        else if (eventType == "tools.ozone.moderation.defs#modEventEscalate")
        {
            ValidateObject(eventValue, ["$type", "comment"]);
            if (root.TryGetProperty("reportAction", out _))
            {
                throw new JsonException("Escalation cannot handle reports.");
            }
            escalate = true;
        }
        else if (eventType is "tools.ozone.moderation.defs#modEventMute" or
            "tools.ozone.moderation.defs#modEventUnmute" or
            "tools.ozone.moderation.defs#modEventMuteReporter" or
            "tools.ozone.moderation.defs#modEventUnmuteReporter")
        {
            bool reporter = eventType.EndsWith("Reporter", StringComparison.Ordinal);
            bool unmute = eventType.Contains("#modEventUnmute", StringComparison.Ordinal);
            ValidateObject(eventValue, unmute ? ["$type", "comment"] : ["$type", "comment", "durationInHours"]);
            if (reporter && subject.RecordUri is not null)
            {
                throw new JsonException("Reporter muting requires an account subject.");
            }
            int? muteHours = unmute ? null : 24;
            if (!unmute && eventValue.TryGetProperty("durationInHours", out _))
            {
                muteHours = checked((int)Integer(eventValue, "durationInHours"));
            }
            mute = new(reporter ? LabelMuteKind.Reporter : LabelMuteKind.Subject, muteHours);
            mute.Validate();
            if (ids.Length != 0 || types.Length != 0 || all)
            {
                throw new JsonException("Mute events cannot close reports.");
            }
        }
        else if (!activity)
        {
            throw new OzoneOperationException("NotImplemented", "This moderation event type is not supported.");
        }
        if (method == "tools.ozone.moderation.emitEvent" && resolve && !root.TryGetProperty("reportAction", out _))
        {
            all = true;
            reportNote = reason;
        }
        string operation = external is null
            ? "ozone:" + Guid.NewGuid().ToString("N")
            : "ozone:external:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                eventType + "\n" + subject.Subject + "\n" + external)));
        Require(await administration.AuthorizeAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Moderate,
            reason: reason,
            cancellationToken: token).ConfigureAwait(false));
        if (external is not null && Require(await events.ReadOzoneOperationAsync(caller, operation, token).ConfigureAwait(false)).Count != 0)
        {
            throw new OzoneOperationException("DuplicateExternalId", "This external identifier has already committed for the event type and subject.");
        }
        if (actions.Count > 0 && keys is null)
        {
            throw new OzoneOperationException("IssuanceClosed", "This host has no label signing key.");
        }
        LabelOzoneEventRequest request = new(
            OperationId: operation,
            ExternalId: external,
            Subject: subject,
            EventJson: eventValue.GetRawText(),
            ModToolJson: tool,
            Reason: reason,
            Actions: [.. actions],
            ReportIds: ids,
            ReportTypes: types,
            AllReports: all,
            ResolveSubject: resolve,
            Automated: automated || Boolean(root, "isAutomated"),
            ReportNote: reportNote,
            Mute: mute,
            IncludeAssignedReports: method == "tools.ozone.moderation.emitEvent" && resolve,
            EscalateSubject: escalate,
            PreserveEscalatedReview: eventType == "tools.ozone.moderation.defs#modEventLabel" &&
                !root.TryGetProperty("reportAction", out _));
        LabelOzoneEvent result;
        using IDisposable? admission = actions.Count > 0 ? host?.TryAdmitAdministrativeIssuance() : null;
        if (actions.Count > 0 && host is not null && admission is null)
        {
            throw new OzoneOperationException("MaintenanceActive", "Label issuance is unavailable during maintenance.");
        }
        if (actions.Count > 0)
        {
            LabelIssuanceService issuance = new(
                issuer: caller.Issuer,
                signingKey: keys!.GetSigningKey(keys.ActiveKeyId),
                store: store,
                authorizer: new LabelTeamAuthorizer(administration, caller),
                policyProvider: policy,
                httpClient: identity,
                timeProvider: clock);
            result = Require(await issuance.ExecuteOzoneEventAsync(caller, request, token).ConfigureAwait(false), external);
        }
        else
        {
            LabelPolicyState state = Require(await policies.GetPolicyStateAsync(caller, token).ConfigureAwait(false));
            result = Require(
                await events.CommitOzoneEventAsync(
                    caller: caller,
                    request: request,
                    policyRevision: state.Revision,
                    labels: [],
                    cancellationToken: token).ConfigureAwait(false),
                external);
        }
        if (activity)
        {
            return Encode(writer =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("activity");
                writer.WriteStartObject();
                writer.WriteNumber("id", checked(result.Id * 1024));
                writer.WriteNumber("reportId", ids[0]);
                writer.WritePropertyName("activity");
                eventValue.WriteTo(writer);
                writer.WriteBoolean("isAutomated", request.Automated);
                writer.WriteString("internalNote", reason);
                writer.WriteString("createdBy", result.Actor);
                writer.WriteString("createdAt", result.CreatedAt);
                writer.WriteEndObject();
                writer.WriteEndObject();
            });
        }
        return method == "tools.ozone.report.closeReports"
            ? Encode(writer =>
            {
                writer.WriteStartObject();
                writer.WriteNumber("closedCount", result.ReportIds.Length);
                writer.WriteStartArray("reportIds");
                foreach (long id in result.ReportIds)
                {
                    writer.WriteNumberValue(id);
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            })
            : Encode(writer => WriteEvent(writer, result));
    }

    /// <summary>Traverses immutable event sources with bounded pages, omitting duplicate Ozone-backed native notes.</summary>
    private async Task VisitEventsAsync(
        LabelAdministrationCaller caller,
        ILabelReportStore reports,
        ILabelModerationStore cases,
        ILabelOzoneStore events,
        Action<OzonePageItem> visit,
        CancellationToken token)
    {
        Dictionary<long, LabelModerationCase> resolvedCases = [];
        long after = 0;
        while (true)
        {
            IReadOnlyList<LabelModerationReport> page = Require(await reports.GetReportsAsync(
                caller: caller,
                afterId: after,
                limit: 100,
                cancellationToken: token).ConfigureAwait(false));
            foreach (LabelModerationReport report in page)
            {
                if (report.Submission.Subject is { RecordUri: not null, Cid: null } &&
                    report.Submission.RecordResolution?.Cid is null)
                {
                    if (!resolvedCases.TryGetValue(report.CaseId, out LabelModerationCase? resolved))
                    {
                        resolved = await ResolveCaseAsync(
                            caller,
                            Require(await cases.GetCaseAsync(caller, report.CaseId, token).ConfigureAwait(false)),
                            events,
                            token).ConfigureAwait(false);
                        resolvedCases.Add(report.CaseId, resolved);
                    }
                    report.Submission.RecordResolution = resolved.RecordResolution;
                }
                visit(new(EventId(report.Id, 0), report.CreatedAt, Encode(writer => WriteReportEvent(
                    writer: writer,
                    id: report.Id,
                    submission: report.Submission,
                    reporter: report.ReportedBy,
                    createdAt: report.CreatedAt))));
            }
            if (page.Count < 100)
            {
                break;
            }
            after = page[^1].Id;
        }
        after = 0;
        while (true)
        {
            IReadOnlyList<LabelPendingModerationReport> page = Require(await events.ReadMutedReportsAsync(
                caller: caller,
                afterId: after,
                limit: 100,
                reportId: null,
                cancellationToken: token).ConfigureAwait(false));
            foreach (LabelPendingModerationReport report in page)
            {
                visit(new(EventId(report.Id, 0), report.CreatedAt, Encode(writer => WriteReportEvent(
                    writer: writer,
                    id: report.Id,
                    submission: report.Submission,
                    reporter: report.ReportedBy,
                    createdAt: report.CreatedAt))));
            }
            if (page.Count < 100)
            {
                break;
            }
            after = page[^1].Id;
        }
        after = 0;
        while (true)
        {
            IReadOnlyList<LabelModerationCase> page = Require(await cases.GetCasesAsync(
                caller: caller,
                afterId: after,
                limit: 100,
                cancellationToken: token).ConfigureAwait(false));
            foreach (LabelModerationCase moderationCase in page)
            {
                LabelModerationCase resolved = resolvedCases.TryGetValue(moderationCase.Id, out LabelModerationCase? retained)
                    ? retained : await ResolveCaseAsync(caller, moderationCase, events, token).ConfigureAwait(false);
                resolvedCases[moderationCase.Id] = resolved;
                long sequence = 0;
                while (true)
                {
                    IReadOnlyList<LabelCaseReceipt> history = Require(await cases.GetCaseHistoryAsync(
                        caller: caller,
                        caseId: moderationCase.Id,
                        afterSequence: sequence,
                        limit: 100,
                        cancellationToken: token).ConfigureAwait(false));
                    foreach (LabelCaseReceipt entry in history.Where(entry => !entry.IsOzoneEvent))
                    {
                        visit(new(EventId(entry.Sequence, 1), entry.CreatedAt, Encode(writer => WriteCaseEvent(writer, entry, resolved.RecordResolution))));
                    }
                    if (history.Count < 100)
                    {
                        break;
                    }
                    sequence = history[^1].Sequence;
                }
            }
            if (page.Count < 100)
            {
                break;
            }
            after = page[^1].Id;
        }
        after = 0;
        while (true)
        {
            IReadOnlyList<LabelOzoneEvent> page = Require(await events.ReadOzoneEventsAsync(
                caller: caller,
                afterId: after,
                limit: 100,
                cancellationToken: token).ConfigureAwait(false));
            foreach (LabelOzoneEvent entry in page)
            {
                LabelRecordResolution[] resolutions = [.. resolvedCases.Values
                    .Where(item => item.Subject.Subject == entry.Subject && item.RecordResolution?.Cid is not null)
                    .Select(item => item.RecordResolution!)
                    .DistinctBy(item => item.Cid)];
                LabelRecordResolution? resolution = resolutions.Length == 1 ? resolutions[0] : null;
                visit(new(EventId(entry.Id, 2), entry.CreatedAt, Encode(writer => WriteEvent(writer, entry, resolution))));
            }
            if (page.Count < 100)
            {
                break;
            }
            after = page[^1].Id;
        }
    }

    /// <summary>Finds the latest exact case without merging distinct submitted versions.</summary>
    private static async Task<LabelModerationCase?> LatestCaseAsync(
        LabelAdministrationCaller caller,
        LabelReportSubject subject,
        ILabelModerationStore cases,
        CancellationToken token)
    {
        LabelModerationCase? latest = null;
        long after = 0;
        while (true)
        {
            IReadOnlyList<LabelModerationCase> page = Require(await cases.GetCasesAsync(
                caller: caller,
                afterId: after,
                limit: 100,
                cancellationToken: token).ConfigureAwait(false));
            foreach (LabelModerationCase moderationCase in page.Where(item =>
                item.Subject.Subject == subject.Subject &&
                ProjectedSubject(item.Subject, item.RecordResolution).Cid == subject.Cid))
            {
                latest = moderationCase;
            }
            if (page.Count < 100)
            {
                return latest;
            }
            after = page[^1].Id;
        }
    }

    /// <summary>Writes a report's current handling status and original subject separately.</summary>
    private static void WriteReport(Utf8JsonWriter writer, LabelModerationReport report, LabelModerationCase moderationCase)
    {
        writer.WriteStartObject();
        writer.WriteNumber("id", report.Id);
        writer.WriteNumber("eventId", EventId(report.Id, 0));
        writer.WriteString("status", ReportStatus(report, moderationCase));
        writer.WritePropertyName("subject");
        WriteSubjectView(writer, report.Submission.Subject);
        writer.WriteString("reportType", report.Submission.ReasonType);
        writer.WriteString("reportedBy", report.ReportedBy.Value);
        writer.WritePropertyName("reporter");
        WriteSubjectView(writer, LabelReportSubject.Account(report.ReportedBy));
        if (report.Submission.Reason is { } reason)
        {
            writer.WriteString("comment", reason);
        }
        writer.WriteString("createdAt", report.CreatedAt);
        writer.WriteString("updatedAt", report.HandledAt ?? report.CreatedAt);
        writer.WriteBoolean("isMuted", false);
        if (report.Submission.RecordResolution is { } resolution)
        {
            writer.WriteStartObject("recordResolution");
            writer.WriteString("status", resolution.Status);
            if (resolution.Cid is { } resolvedCid)
            {
                writer.WriteString("cid", resolvedCid);
            }
            writer.WriteString("observedAt", resolution.ObservedAt);
            writer.WriteEndObject();
        }
        if (moderationCase.Subject.RecordUri is null || moderationCase.Subject.Cid is not null ||
            moderationCase.RecordResolution?.Cid is not null)
        {
            writer.WritePropertyName("subjectStatus");
            WriteStatus(writer, moderationCase);
        }
        writer.WriteEndObject();
    }

    /// <summary>Writes immutable muted submissions without inventing a case, assignment or review state.</summary>
    private static void WriteMutedReport(Utf8JsonWriter writer, LabelPendingModerationReport report)
    {
        writer.WriteStartObject();
        writer.WriteNumber("id", report.Id);
        writer.WriteNumber("eventId", EventId(report.Id, 0));
        writer.WriteString("status", "open");
        writer.WritePropertyName("subject");
        WriteSubjectView(writer, report.Submission.Subject);
        writer.WriteString("reportType", report.Submission.ReasonType);
        writer.WriteString("reportedBy", report.ReportedBy.Value);
        writer.WritePropertyName("reporter");
        WriteSubjectView(writer, LabelReportSubject.Account(report.ReportedBy));
        if (report.Submission.Reason is { } reason)
        {
            writer.WriteString("comment", reason);
        }
        writer.WriteString("createdAt", report.CreatedAt);
        writer.WriteString("updatedAt", report.CreatedAt);
        writer.WriteBoolean("isMuted", true);
        writer.WriteEndObject();
    }

    /// <summary>Writes a string-subject view that can represent URI-only reports faithfully.</summary>
    private static void WriteSubjectView(Utf8JsonWriter writer, LabelReportSubject subject)
    {
        writer.WriteStartObject();
        writer.WriteString("type", subject.RecordUri is null ? "account" : "record");
        writer.WriteString("subject", subject.Subject);
        writer.WriteEndObject();
    }

    /// <summary>Writes submitted protocol scope, retaining the existing URI-only repo-reference convention.</summary>
    private static void WriteReference(Utf8JsonWriter writer, LabelReportSubject subject)
    {
        writer.WriteStartObject();
        ReportSubjectView view = ModerationWire.ProtocolSubjectView(subject);
        writer.WriteString("$type", view.Type);
        if (subject.Cid is null)
        {
            writer.WriteString("did", subject.Subject);
        }
        else
        {
            writer.WriteString("uri", subject.Subject);
            writer.WriteString("cid", subject.Cid.ToString());
        }
        writer.WriteEndObject();
    }

    /// <summary>Writes an explicitly native case status rather than asserting an unsupported takedown or appeal outcome.</summary>
    private static void WriteStatus(Utf8JsonWriter writer, LabelModerationCase moderationCase)
    {
        writer.WriteStartObject();
        WriteStatusProperties(writer, moderationCase);
        writer.WriteEndObject();
    }

    /// <summary>Writes the retained status properties for use in detailed and list views.</summary>
    private static void WriteStatusProperties(Utf8JsonWriter writer, LabelModerationCase moderationCase)
    {
        writer.WriteNumber("id", moderationCase.Id);
        writer.WritePropertyName("subject");
        WriteReference(writer, ProjectedSubject(moderationCase.Subject, moderationCase.RecordResolution));
        WriteOriginalSubject(writer, moderationCase.Subject, moderationCase.RecordResolution);
        writer.WriteString("createdAt", moderationCase.CreatedAt);
        writer.WriteString("updatedAt", moderationCase.UpdatedAt);
        writer.WriteString("reviewState", ReviewState(moderationCase));
    }

    /// <summary>Writes only a status actually retained for this exact display subject.</summary>
    private static void WriteModeration(Utf8JsonWriter writer, LabelModerationCase? status)
    {
        writer.WriteStartObject("moderation");
        if (status is not null)
        {
            writer.WritePropertyName("subjectStatus");
            WriteStatus(writer, status);
        }
        writer.WriteEndObject();
    }

    /// <summary>Writes exact signed JSON without regenerating signatures or signed fields.</summary>
    private static void WriteLabels(Utf8JsonWriter writer, StoredLabel[] labels)
    {
        writer.WriteStartArray("labels");
        foreach (StoredLabel label in labels)
        {
            using JsonDocument document = JsonDocument.Parse(label.SignedLabel.GetJsonBytes());
            document.RootElement.WriteTo(writer);
        }
        writer.WriteEndArray();
    }

    /// <summary>Writes the immutable Ozone event that committed with its labels and selected report resolutions.</summary>
    private static void WriteEvent(Utf8JsonWriter writer, LabelOzoneEvent entry, LabelRecordResolution? resolution = null)
    {
        writer.WriteStartObject();
        writer.WriteNumber("id", EventId(entry.Id, 2));
        writer.WritePropertyName("event");
        using JsonDocument document = JsonDocument.Parse(entry.EventJson);
        if (document.RootElement.GetProperty("$type").GetString() == "tools.ozone.report.defs#closeActivity")
        {
            writer.WriteStartObject();
            writer.WriteString("$type", "tools.ozone.moderation.defs#modEventAcknowledge");
            writer.WriteEndObject();
        }
        else
        {
            document.RootElement.WriteTo(writer);
        }
        writer.WritePropertyName("subject");
        Cid? cid = entry.Cid is null ? null : new Cid(entry.Cid);
        LabelReportSubject subject = entry.Subject.StartsWith("at://", StringComparison.Ordinal)
            ? LabelReportSubject.Record(new AtUri(entry.Subject), cid)
            : LabelReportSubject.Account(new Did(entry.Subject));
        WriteReference(writer, ProjectedSubject(subject, resolution));
        WriteOriginalSubject(writer, subject, resolution);
        writer.WriteStartArray("subjectBlobCids");
        writer.WriteEndArray();
        writer.WriteString("createdBy", entry.Actor);
        writer.WriteString("createdAt", entry.CreatedAt);
        if (entry.ModToolJson is { } tool)
        {
            writer.WritePropertyName("modTool");
            using JsonDocument toolDocument = JsonDocument.Parse(tool);
            toolDocument.RootElement.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    /// <summary>Projects a retained native report as an immutable report event.</summary>
    private static void WriteReportEvent(
        Utf8JsonWriter writer,
        long id,
        LabelReportSubmission submission,
        Did reporter,
        DateTimeOffset createdAt)
    {
        writer.WriteStartObject();
        writer.WriteNumber("id", EventId(id, 0));
        writer.WriteStartObject("event");
        writer.WriteString("$type", "tools.ozone.moderation.defs#modEventReport");
        writer.WriteString("reportType", submission.ReasonType);
        if (submission.Reason is { } reason)
        {
            writer.WriteString("comment", reason);
        }
        writer.WriteEndObject();
        writer.WritePropertyName("subject");
        WriteReference(writer, ProjectedSubject(submission.Subject, submission.RecordResolution));
        WriteOriginalSubject(writer, submission.Subject, submission.RecordResolution);
        writer.WriteStartArray("subjectBlobCids");
        writer.WriteEndArray();
        writer.WriteString("createdBy", reporter.Value);
        writer.WriteString("createdAt", createdAt);
        writer.WriteEndObject();
    }

    /// <summary>Projects native escalation as its matching Ozone event and other workflow audit as comments.</summary>
    private static void WriteCaseEvent(Utf8JsonWriter writer, LabelCaseReceipt entry, LabelRecordResolution? resolution = null)
    {
        writer.WriteStartObject();
        writer.WriteNumber("id", EventId(entry.Sequence, 1));
        writer.WriteStartObject("event");
        writer.WriteString("$type", entry.Action == LabelCaseAction.Escalate
            ? "tools.ozone.moderation.defs#modEventEscalate" : "tools.ozone.moderation.defs#modEventComment");
        writer.WriteString("comment", entry.Reason);
        writer.WriteEndObject();
        writer.WritePropertyName("subject");
        WriteReference(writer, ProjectedSubject(entry.Case.Subject, resolution ?? entry.Case.RecordResolution));
        WriteOriginalSubject(writer, entry.Case.Subject, resolution ?? entry.Case.RecordResolution);
        writer.WriteStartArray("subjectBlobCids");
        writer.WriteEndArray();
        writer.WriteString("createdBy", entry.Actor.Value);
        writer.WriteString("createdAt", entry.CreatedAt);
        writer.WriteEndObject();
    }

    /// <summary>Keeps at most one result page and lookahead while traversing provider pages.</summary>
    private static void Consider(
        HttpContext context,
        LabelAdministrationCaller caller,
        List<OzonePageItem> items,
        OzonePageItem item)
    {
        int limit = Limit(context);
        bool descending = Direction(context);
        if (context.Request.Query.ContainsKey("cursor"))
        {
            OzonePageItem boundary = Boundary(context, caller);
            int comparison = Compare(item, boundary);
            if (descending ? comparison >= 0 : comparison <= 0)
            {
                return;
            }
        }
        items.Add(item);
        items.Sort((first, second) => descending ? Compare(second, first) : Compare(first, second));
        if (items.Count > limit + 1)
        {
            items.RemoveAt(items.Count - 1);
        }
    }

    /// <summary>Writes a bounded live page and a caller/filter-bound chronological cursor.</summary>
    private static byte[] Page(
        HttpContext context,
        LabelAdministrationCaller caller,
        string name,
        List<OzonePageItem> items,
        int unresolved = 0) =>
        Encode(writer =>
        {
            int limit = Limit(context);
            if (context.Request.Query.ContainsKey("cursor"))
            {
                _ = Boundary(context, caller);
            }
            writer.WriteStartObject();
            writer.WriteStartArray(name);
            foreach (OzonePageItem item in items.Take(limit))
            {
                using JsonDocument document = JsonDocument.Parse(item.Payload);
                document.RootElement.WriteTo(writer);
            }
            writer.WriteEndArray();
            if (unresolved > 0)
            {
                context.Response.Headers["Warning"] = "199 - \"Unresolved record references omitted; use native Xenon reports for complete results\"";
                writer.WriteNumber("unresolvedRecordCount", unresolved);
            }
            if (items.Count > limit)
            {
                OzonePageItem last = items[limit - 1];
                writer.WriteString(
                    "cursor",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    FilterHash(context, caller) + "|" + last.Time.ToString("O", CultureInfo.InvariantCulture) + "|" +
                    last.Id.ToString(CultureInfo.InvariantCulture))));
            }
            writer.WriteEndObject();
        });

    /// <summary>Validates continuations even when the query has no matching candidates.</summary>
    private static OzonePageItem Boundary(HttpContext context, LabelAdministrationCaller caller)
    {
        string[] parts = Encoding.UTF8.GetString(Convert.FromBase64String(Query(context, "cursor"))).Split('|');
        if (parts.Length != 3 || parts[0] != FilterHash(context, caller))
        {
            throw new JsonException("The cursor does not match this caller and query.");
        }
        long id = long.Parse(parts[2], CultureInfo.InvariantCulture);
        ArgumentOutOfRangeException.ThrowIfLessThan(id, 1);
        return new(id, DateTimeOffset.Parse(parts[1], CultureInfo.InvariantCulture), []);
    }

    /// <summary>Sorts stable identities only after chronological ordering.</summary>
    private static int Compare(OzonePageItem left, OzonePageItem right) =>
        left.Time != right.Time ? left.Time.CompareTo(right.Time) : left.Id.CompareTo(right.Id);

    /// <summary>Binds live continuations to their authenticated issuer, actor, endpoint and complete filters.</summary>
    private static string FilterHash(HttpContext context, LabelAdministrationCaller caller) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            caller.Issuer.Value + "\n" + caller.Actor.Value + "\n" + context.Request.Path + "\n" +
            string.Join(
                '\n',
                context.Request.Query.Where(pair => pair.Key != "cursor").OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value.ToString())))));

    /// <summary>Applies exact subject, repository, collection and subject-type filters.</summary>
    private static bool MatchesSubject(HttpContext context, LabelReportSubject subject) =>
        (!context.Request.Query.ContainsKey("subject") || Query(context, "subject") == subject.Subject) &&
        (!context.Request.Query.ContainsKey("did") || Query(context, "did") == subject.Repository.Value) &&
        (!context.Request.Query.ContainsKey("subjectType") || Query(context, "subjectType") == (subject.RecordUri is null ? "account" : "record")) &&
        (!context.Request.Query.ContainsKey("collections") || subject.RecordUri is { } uri &&
            context.Request.Query["collections"].Contains(uri.Collection!.ToString(), StringComparer.Ordinal));

    /// <summary>Maps actual report handling and native assignment rather than inventing queue state.</summary>
    private static string ReportStatus(LabelModerationReport report, LabelModerationCase moderationCase)
    {
        if (report.HandledAt is not null)
        {
            return "closed";
        }
        if (moderationCase.Status == LabelCaseStatus.Escalated)
        {
            return "escalated";
        }
        return moderationCase.Assignee is null ? "open" : "assigned";
    }

    /// <summary>Maps durable native review state to the corresponding Ozone subject state.</summary>
    private static string ReviewState(LabelModerationCase moderationCase) =>
        moderationCase.Status switch
        {
            LabelCaseStatus.Resolved => "tools.ozone.moderation.defs#reviewClosed",
            LabelCaseStatus.Escalated => "tools.ozone.moderation.defs#reviewEscalated",
            _ => "tools.ozone.moderation.defs#reviewOpen"
        };

    /// <summary>Allocates disjoint, deterministic safe-integer namespaces for the three immutable event sources.</summary>
    private static long EventId(long id, int kind)
    {
        if (id <= 0 || id > 9_007_199_254_740_991 / 4)
        {
            throw new OzoneOperationException("ServiceUnavailable", "The event identity exceeds Ozone's safe integer range.");
        }
        return checked(id * 4 + kind);
    }

    /// <summary>Encodes structured JSON directly without reflection-based object serialization.</summary>
    private static byte[] Encode(Action<Utf8JsonWriter> write)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            write(writer);
        }
        return stream.ToArray();
    }

    /// <summary>Preserves classified provider failures rather than returning success-shaped empty data.</summary>
    private static T Require<T>(LabelServiceResult<T> result, string? externalId = null) where T : class
    {
        if (!result.Succeeded)
        {
            string code = externalId is not null && result.Error.Code == LabelServiceErrorCode.IdempotencyConflict
                ? "DuplicateExternalId" : result.Error.Code.ToString();
            throw new OzoneOperationException(code, result.Error.Message);
        }
        return result.Result;
    }

    /// <summary>Rejects unknown, repeated scalar or unbounded query parameters.</summary>
    private static void ValidateQuery(HttpContext context, string[] allowed)
    {
        if (context.Request.QueryString.Value?.Length > 4096 ||
            context.Request.Query.Any(pair => !allowed.Contains(pair.Key, StringComparer.Ordinal) ||
                (pair.Key switch
                {
                    "dids" or "uris" or "keys" => pair.Value.Count > 100,
                    "types" or "reportTypes" or "collections" => pair.Value.Count > 20,
                    _ => pair.Value.Count != 1
                })))
        {
            throw new JsonException("The Ozone query parameters are unsupported or exceed their bounds.");
        }
        _ = Limit(context);
        _ = Direction(context);
    }

    /// <summary>Rejects unknown or duplicate fields so unsupported intent never succeeds partially.</summary>
    private static void ValidateObject(JsonElement element, string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("An object is required.");
        }
        HashSet<string> names = [];
        if (element.EnumerateObject().Any(property =>
            !allowed.Contains(property.Name, StringComparer.Ordinal) || !names.Add(property.Name)))
        {
            throw new JsonException("An unsupported or duplicate Ozone field was supplied.");
        }

    }

    /// <summary>Rejects ambiguous fields throughout a bounded wire document, including extensible metadata.</summary>
    private static void ValidateJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = [];
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException("Duplicate JSON properties are not supported.");
                }
                ValidateJson(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateJson(item);
            }
        }
    }

    /// <summary>Reads an optional scalar query value.</summary>
    private static string Query(HttpContext context, string name, string fallback = "") =>
        context.Request.Query.ContainsKey(name) ? context.Request.Query[name].ToString() : fallback;

    /// <summary>Reads a required scalar query value.</summary>
    private static string RequiredQuery(HttpContext context, string name) =>
        context.Request.Query.ContainsKey(name) ? Query(context, name) : throw new JsonException("A required query parameter is missing.");

    /// <summary>Reads an integral query value.</summary>
    private static long Number(HttpContext context, string name) => long.Parse(RequiredQuery(context, name), CultureInfo.InvariantCulture);

    /// <summary>Validates the protocol's bounded result page size.</summary>
    private static int Limit(HttpContext context)
    {
        int limit = int.Parse(Query(context, "limit", "50"), CultureInfo.InvariantCulture);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
        return limit;
    }

    /// <summary>Validates chronological query direction.</summary>
    private static bool Direction(HttpContext context) => Query(context, "sortDirection", "desc") switch
    {
        "desc" => true,
        "asc" => false,
        _ => throw new JsonException("The sort direction is invalid.")
    };

    /// <summary>Reads bounded required wire text.</summary>
    private static string Text(JsonElement element, string name)
    {
        JsonElement value = Field(element, name);
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("A string is required.");
        }
        return LabelInput.ValidateText(value.GetString() ?? throw new JsonException("Required text is null."), name, 4096);
    }

    /// <summary>Reads required JSON fields with an explicit malformed-input error.</summary>
    private static JsonElement Field(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement field)
            ? field : throw new JsonException("A required JSON field is missing.");

    /// <summary>Reads integral JSON values with a classified invalid-input failure.</summary>
    private static long Integer(JsonElement element, string name)
    {
        JsonElement value = Field(element, name);
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException("An integer is required.");
        }
        return value.GetInt64();
    }

    /// <summary>Reads optional wire text without substituting a value for null input.</summary>
    private static string? OptionalText(JsonElement element, string name) =>
        element.TryGetProperty(name, out _) ? Text(element, name) : null;

    /// <summary>Reads a strict optional Boolean.</summary>
    private static bool Boolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            return false;
        }
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new JsonException("A Boolean is required.");
        }
        return value.GetBoolean();
    }

    /// <summary>Reads bounded distinct report identifiers.</summary>
    private static long[] Numbers(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out JsonElement array) && array.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("An array is required.");
        }
        long[] values = element.TryGetProperty(name, out JsonElement value)
            ? [.. value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Number
                ? item.GetInt64() : throw new JsonException("An integer is required."))] : [];
        if (values.Length > 100 || values.Any(id => id <= 0) || values.Distinct().Count() != values.Length)

        {
            throw new JsonException("Report identifiers must be distinct, positive and bounded.");
        }
        return values;
    }

    /// <summary>Reads bounded wire arrays without ignoring missing required collections.</summary>
    private static string[] Strings(JsonElement element, string name, bool required = false)
    {
        if (!element.TryGetProperty(name, out JsonElement value))

        {
            return required ? throw new JsonException("A required array is missing.") : [];
        }
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("An array is required.");
        }
        string[] values = [.. value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String
            ? item.GetString() ?? throw new JsonException("Array text is null.") : throw new JsonException("A string is required."))];
        if (values.Length > 100 || values.Any(item => Encoding.UTF8.GetByteCount(item) > 4096))

        {
            throw new JsonException("The wire array exceeds its bounds.");
        }
        return values;
    }
    /// <summary>Carries a classified adapter error without exposing credentials or untrusted request bodies.</summary>
    [SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Private dispatch control flow always requires an XRPC error code.")]
    [SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "Caught entirely inside the private adapter dispatch boundary.")]
    [SuppressMessage("CodeSmell", "S3871:Exception types should be public", Justification = "Caught entirely inside the private adapter dispatch boundary.")]
    private sealed class OzoneOperationException(string code, string message) : Exception(message)
    {
        /// <summary>Gets the classified XRPC failure code.</summary>
        internal string Code { get; } = code;
    }
}

/// <summary>Retains one bounded page candidate and its chronological identity.</summary>
internal sealed record OzonePageItem(long Id, DateTimeOffset Time, byte[] Payload);
