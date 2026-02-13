// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using idunno.AtProto.Admin;
using idunno.AtProto.Moderation;
using idunno.AtProto.Repo;
using idunno.AtProto;
using idunno.Bluesky.Moderation;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Creates and sends a moderation report to the specified <paramref name="labelerDid"/>.
        /// </summary>
        /// <param name="labelerDid">The <see cref="Did"/> of the labeler to report to.</param>
        /// <param name="subject">The subject of the report, a <see cref="StrongReference"/> to a record, or a <see cref="RepoReference"/> for an actor.</param>
        /// <param name="reportType">The reason for the report.</param>
        /// <param name="reason">Any notes to justify the report.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="labelerDid"/> or <paramref name="subject"/> is <see langword="null"/>.
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
            SubjectType subject,
            ReportType reportType,
            string? reason,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(labelerDid);
            ArgumentNullException.ThrowIfNull(subject);

            if (reason is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, 20000);
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await CreateModerationReport(
                labelerDid: labelerDid,
                reportSubject: subject,
                reasonType : WellKnown.ReportType[reportType],
                reason: reason,
                cancellationToken: cancellationToken).ConfigureAwait(false);

        }
    }
}
