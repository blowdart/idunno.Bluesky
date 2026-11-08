// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.AspNet.Labeler;

internal sealed partial class AdministrationDispatcher
{
    /// <summary>Shares durable, audited Ozone-compatible mute operations with the native administration UI.</summary>
    private async Task<AdministrationResponse> DispatchMutesAsync(
        LabelAdministrationCaller caller,
        AdministrationCommand command,
        ModerationCommand input,
        ILabelPolicyStore policies,
        CancellationToken token)
    {
        if (store is not ILabelOzoneStore mutes)
        {
            return new(false, "UnsupportedMutes", "This provider does not support temporary moderation mutes.");
        }
        if (command.Method == "getMutes")
        {
            LabelServiceResult<IReadOnlyList<LabelMuteEntry>> listed = await mutes.ReadMutesAsync(
                caller: caller,
                afterEventId: input.AfterMuteEventId,
                limit: command.Limit,
                cancellationToken: token).ConfigureAwait(false);
            return listed.Succeeded ? new(true, Moderation: new(Mutes: [.. listed.Result])) : Failure(listed.Error);
        }
        string target = input.MuteSubject ?? throw new ArgumentException("A mute target is required.");
        LabelReportSubject subject = target.StartsWith("at://", StringComparison.Ordinal)
            ? LabelReportSubject.Record(new AtUri(target))
            : LabelReportSubject.Account(new Did(target));
        LabelMuteKind kind = input.MuteKind ?? throw new ArgumentException("Choose a mute kind.");
        LabelMuteChange change = new(kind, input.MuteDurationHours, input.ExpectedMuteEventId);
        change.Validate();
        if (kind == LabelMuteKind.Reporter && subject.RecordUri is not null)
        {
            throw new ArgumentException("Reporter mutes require an account DID.");
        }
        string eventType = (kind, input.MuteDurationHours) switch
        {
            (LabelMuteKind.Subject, null) => "tools.ozone.moderation.defs#modEventUnmute",
            (LabelMuteKind.Subject, _) => "tools.ozone.moderation.defs#modEventMute",
            (LabelMuteKind.Reporter, null) => "tools.ozone.moderation.defs#modEventUnmuteReporter",
            _ => "tools.ozone.moderation.defs#modEventMuteReporter"
        };
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("$type", eventType);
            writer.WriteString("comment", command.Reason);
            if (input.MuteDurationHours is { } hours)
            {
                writer.WriteNumber("durationInHours", hours);
            }
            writer.WriteEndObject();
        }
        LabelServiceResult<LabelPolicyState> gate = await policies.GetPolicyStateAsync(caller, token).ConfigureAwait(false);
        if (!gate.Succeeded)
        {
            return Failure(gate.Error);
        }
        LabelOzoneEventRequest request = new(
            OperationId: command.OperationId,
            ExternalId: null,
            Subject: subject,
            EventJson: System.Text.Encoding.UTF8.GetString(buffer.ToArray()),
            ModToolJson: null,
            Reason: command.Reason,
            Actions: [],
            ReportIds: [],
            ReportTypes: [],
            AllReports: false,
            ResolveSubject: false,
            Automated: false,
            ReportNote: null,
            Mute: change);
        LabelServiceResult<LabelOzoneEvent> result = await mutes.CommitOzoneEventAsync(
            caller: caller,
            request: request,
            policyRevision: gate.Result.Revision,
            labels: [],
            cancellationToken: token).ConfigureAwait(false);
        if (!result.Succeeded && result.Error.Code == LabelServiceErrorCode.IdempotencyConflict)
        {
            LabelServiceResult<IReadOnlyList<LabelOzoneEvent>> previous = await mutes.ReadOzoneOperationAsync(
                caller: caller, operationId: command.OperationId, cancellationToken: token).ConfigureAwait(false);
            if (previous.Succeeded && previous.Result is [{ } prior] &&
                prior.Actor == caller.Actor.Value && prior.Subject == subject.Subject &&
                prior.EventJson == request.EventJson)
            {
                return new(true);
            }
        }
        return result.Succeeded ? new(true) : Failure(result.Error);
    }
}
