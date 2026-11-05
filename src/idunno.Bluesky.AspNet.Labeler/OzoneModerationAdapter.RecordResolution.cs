// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.AspNet.Labeler;

internal sealed partial class OzoneModerationAdapter
{
    private int _resolutionAttempts;

    /// <summary>Recovers legacy or failed observations under live staff authority and pins only the first success.</summary>
    private async Task<LabelModerationCase> ResolveCaseAsync(
        LabelAdministrationCaller caller,
        LabelModerationCase moderationCase,
        ILabelOzoneStore events,
        CancellationToken token,
        bool retryRecentlyObserved = false)
    {
        if (moderationCase.Subject is not { RecordUri: { } uri, Cid: null } ||
            moderationCase.RecordResolution?.Cid is not null)
        {
            return moderationCase;
        }

        if (_resolutionAttempts >= 3 ||
            !retryRecentlyObserved && moderationCase.RecordResolution is { } previous &&
                previous.ObservedAt > clock.GetUtcNow().AddMinutes(-5))
        {
            return moderationCase;
        }

        _resolutionAttempts++;
        LabelRecordResolution resolution = await ReportRecordResolver.ResolveAsync(uri, identity, clock, token).ConfigureAwait(false);
        return Require(await events.ResolveRecordCaseAsync(caller, moderationCase.Id, resolution, token).ConfigureAwait(false));
    }

    /// <summary>Supplies a real Ozone reference without changing native report evidence or label targeting.</summary>
    private static LabelReportSubject ProjectedSubject(LabelReportSubject subject, LabelRecordResolution? resolution) =>
        subject is { RecordUri: { } uri, Cid: null } && resolution?.Cid is { } cid
            ? LabelReportSubject.Record(uri, new Cid(cid)) : subject;

    /// <summary>Distinguishes a projected strong reference from the immutable URI-only evidence.</summary>
    private static void WriteOriginalSubject(
        Utf8JsonWriter writer,
        LabelReportSubject subject,
        LabelRecordResolution? resolution)
    {
        if (subject is not { RecordUri: { } uri, Cid: null } || resolution?.Cid is null)
        {
            return;
        }

        writer.WriteStartObject("originalSubject");
        writer.WriteString("uri", uri.ToString());
        writer.WriteEndObject();
        writer.WriteStartObject("subjectResolution");
        writer.WriteString("cid", resolution.Cid);
        writer.WriteString("observedAt", resolution.ObservedAt);
        writer.WriteString("status", resolution.Status);
        writer.WriteEndObject();
    }
}
