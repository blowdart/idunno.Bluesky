// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Admin;
using idunno.AtProto.Moderation;
using idunno.AtProto.Repo;

namespace idunno.AtProto
{
    public partial class AtProtoAgent : Agent
    {
        /// <summary>
        /// Creates and sends a moderation report to the specified <paramref name="labelerDid"/>.
        /// </summary>
        /// <param name="labelerDid">The <see cref="Did"/> of the labeler to report to.</param>
        /// <param name="reportSubject">The subject of the report, a <see cref="StrongReference"/> to a record, or a <see cref="RepoReference"/> for an actor.</param>
        /// <param name="reasonType">The reason for the report.</param>
        /// <param name="reason">Any notes to justify the report.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any of <paramref name="labelerDid"/>, <paramref name="reportSubject"/>, <paramref name="reasonType"/> are <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="reason"/> is not <see langword="null"/> and is &gt; 20000 characters.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        /// <remarks>
        /// <para>
        ///     Clients should not send reports to Labelers which do not match the subject and report type metadata in their declaration record.
        ///     Labeler declarations can be retrieved 
        /// </para>
        /// </remarks>
        public async Task<AtProtoHttpResult<ModerationReport>> CreateModerationReport(
            Did labelerDid,
            SubjectType reportSubject,
            string reasonType,
            string? reason,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(labelerDid);
            ArgumentNullException.ThrowIfNull(reportSubject);
            ArgumentNullException.ThrowIfNull(reasonType);

            if (reason is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, 20000);
            }

            if (!IsAuthenticated)
            {
                Logger.ApplyWritesFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticationRequiredException();
            }

            return await AtProtoServer.CreateModerationReport(
                labelerDid: labelerDid,
                subject: reportSubject,
                reportType: reasonType,
                reason: reason,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
