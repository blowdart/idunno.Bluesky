// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler;
using idunno.AtProto;
using idunno.Bluesky.Labeler.Issuance;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.AspNet.Labeler;

internal sealed partial class AdministrationDispatcher
{
    /// <summary>Resolves an audit actor using the same bidirectionally verified account resolution as manual labeling.</summary>
    internal async Task<AdministrationResponse> ResolveAuditActorAsync(string value, CancellationToken token)
    {
        string input = value.StartsWith('@') ? value[1..] : value;
        if (!AtIdentifier.TryParse(input, out AtIdentifier? identifier))
        {
            return new(false, "InvalidRequest", "Enter an actor DID or handle.");
        }

        if (identifier is Did did)
        {
            return new(Succeeded: true, Target: did.Value);
        }

        LabelServiceResult<LabelTarget> resolved = await new LabelTargetResolver(httpClient, NullLoggerFactory.Instance)
            .ResolveAsync(LabelTarget.Account(identifier), token).ConfigureAwait(false);
        return resolved.Succeeded
            ? new(Succeeded: true, Target: resolved.Result.Repository.Value)
            : Failure(resolved.Error);
    }

    /// <summary>Records an authorized browser export start and returns the fixed sequence cutoff without exposing a remote administrative method.</summary>
    internal async Task<AdministrationResponse> BeginAuditExportAsync(
        LabelAdministrationCaller caller,
        LabelTableQuery query,
        CancellationToken token)
    {
        if (store is not ILabelTableStore tables)
        {
            return new(false, "UnsupportedTable", "This storage provider does not support audit exports.");
        }

        LabelServiceResult<LabelAdministrationReceipt> result = await tables.BeginAuditExportAsync(caller, query, token).ConfigureAwait(false);
        return result.Succeeded ? new(Succeeded: true, Sequence: result.Result.Sequence - 1) : Failure(result.Error);
    }

    /// <summary>Projects one authorized sequence-bounded export page without loading or recounting the entire trail.</summary>
    internal async Task<AdministrationResponse> ReadAuditExportPageAsync(
        LabelAdministrationCaller caller,
        LabelTableQuery query,
        CancellationToken token)
    {
        if (store is not ILabelTableStore tables)
        {
            return new(false, "UnsupportedTable", "This storage provider does not support audit exports.");
        }

        LabelServiceResult<IReadOnlyList<LabelAdministrationReceipt>> result = await tables.ReadAuditExportPageAsync(caller, query, token).ConfigureAwait(false);
        return result.Succeeded ? new(Succeeded: true, Audit: result.Result.Select(AuditView).ToArray()) : Failure(result.Error);
    }

    /// <summary>Projects a counted provider page without adding a remote administrative method or weakening live authority checks.</summary>
    internal async Task<AdministrationResponse> ReadTableAsync(
        LabelAdministrationCaller caller,
        LabelTableQuery query,
        CancellationToken token)
    {
        if (store is not ILabelTableStore tables)
        {
            return new(false, "UnsupportedTable", "This storage provider does not support sorted, counted table pages.");
        }

        LabelServiceResult<LabelTablePage> result = await tables.ReadTableAsync(caller, query, token).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return Failure(result.Error);
        }

        LabelTablePage page = result.Result;
        return new(
            Succeeded: true,
            Members: page.Team?.Select(member => new AdministrationMemberView(member.Did.Value, member.Role, member.Active, member.Removed)).ToArray(),
            Audit: page.Audit?.Select(AuditView).ToArray(),
            Moderation: new(
                Cases: page.Cases?.Select(CaseView).ToArray(),
                Reports: page.Reports?.Select(ReportView).ToArray(),
                Appeals: page.Appeals?.Select(AppealView).ToArray(),
                PendingAppealCaseIds: page.PendingAppealCaseIds?.ToArray()),
            TotalCount: page.Total,
            PageNumber: page.Page,
            AuthorSummaries: page.AuthorSummaries?.Select(summary => new AdministrationAuthorSummaryView(
                summary.Did, summary.Reports, summary.Records, summary.ReportKinds)).ToArray());
    }
}
