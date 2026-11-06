// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <inheritdoc/>
    public Task<LabelServiceResult<LabelModerationCase>> ResolveRecordCaseAsync(
        LabelAdministrationCaller caller,
        long caseId,
        LabelRecordResolution resolution,
        CancellationToken cancellationToken) =>
        ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Observe URI-only report reference.",
            action: async (connection, transaction, _) =>
            {
                ModerationCaseData? current = await ReadCaseAsync(connection, transaction, caseId, cancellationToken).ConfigureAwait(false);
                if (current is null)
                {
                    return CaseFailure<LabelModerationCase>(LabelServiceErrorCode.CaseConflict, "The case no longer exists.");
                }

                if (current.Cid is not null || !current.Subject.StartsWith("at://", StringComparison.Ordinal) ||
                    current.RecordResolution?.Cid is not null)
                {
                    return LabelServiceResult<LabelModerationCase>.Success(current.ToCase());
                }

                ModerationCaseData next = current with { RecordResolution = resolution };
                await SaveCaseAsync(
                    connection: connection,
                    transaction: transaction,
                    next: next,
                    subject: current.ToCase().Subject,
                    insert: false,
                    token: cancellationToken).ConfigureAwait(false);
                return LabelServiceResult<LabelModerationCase>.Success(next.ToCase());
            },
            token: cancellationToken);
}
