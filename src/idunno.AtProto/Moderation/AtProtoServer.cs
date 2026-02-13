// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using idunno.AtProto.Admin;
using idunno.AtProto.Authentication;
using idunno.AtProto.Moderation;
using idunno.AtProto.Moderation.Model;
using idunno.AtProto.Repo;

namespace idunno.AtProto
{
    public static partial class AtProtoServer
    {
        // https://docs.bsky.app/docs/api/com-atproto-moderation-create-report
        internal const string ModerationCreateReportEndpoint = "/xrpc/com.atproto.moderation.createReport";

        /// <summary>
        /// Creates and sends a moderation report to the specified <paramref name="labelerDid"/>.
        /// </summary>
        /// <param name="labelerDid">The <see cref="Did"/> of the labeler to report to.</param>
        /// <param name="subject">The subject of the report, a <see cref="StrongReference"/> to a record, or a <see cref="RepoReference"/> for an actor.</param>
        /// <param name="reportType">The reason for the report.</param>
        /// <param name="reason">Any notes to justify the report.</param>
        /// <param name="service">The service to create the record on.</param>
        /// <param name="accessCredentials">Access credentials for the specified service.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any of <paramref name="labelerDid"/>, <paramref name="subject"/>, <paramref name="reportType"/>, <paramref name="service"/> 
        /// <paramref name="accessCredentials"/> or <paramref name="httpClient"/> are <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="reason"/> is not <see langword="null"/> and is &gt; 20000 characters.</exception>
        /// <remarks>
        /// <para>
        ///     Clients should not send reports to Labelers which do not match the subject and report type metadata in their declaration record.
        ///     Labeler declarations can be retrieved 
        /// </para>
        /// </remarks>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<ModerationReport>> CreateModerationReport(
            Did labelerDid,
            SubjectType subject,
            string reportType,
            string? reason,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(labelerDid);
            ArgumentNullException.ThrowIfNull(subject);
            ArgumentNullException.ThrowIfNull(reportType);

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (reason is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, 20000);
            }

            string atProtoProxy = $"{labelerDid}#atproto_labeler";

            CreateReportRequest request = new()
            {
                ReasonType = reportType,
                Subject = subject,
                Reason = reason
            };

            AtProtoHttpClient<ModerationReport> client = new(atProtoProxy, loggerFactory);

            return await client.Post(
                service: service,
                endpoint: ModerationCreateReportEndpoint,
                record: request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: AtProtoJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
