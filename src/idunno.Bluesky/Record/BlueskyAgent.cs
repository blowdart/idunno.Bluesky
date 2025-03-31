// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;
using idunno.Bluesky.Record;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Gets the post record for the specified <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri" /> of the post to return the <see cref="PostRecord"/> for.</param>
        /// <param name="cid">An optional <see cref="Cid" /> of the post to return the <see cref="PostRecord"/> for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        public async Task<AtProtoHttpResult<PostRecord>> GetPostRecord(
            AtUri uri,
            Cid? cid = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            AtProtoHttpResult<PostRecord> result = await BlueskyServer.GetPostRecord(
                uri,
                cid,
                service: AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                if (IsAuthenticated)
                {
                    Logger.GetPostRecordSucceeded(_logger, Did, uri, cid, AuthenticatedOrUnauthenticatedServiceUri);
                }
                else
                {
                    Logger.GetPostRecordSucceededAnon(_logger, uri, cid, AuthenticatedOrUnauthenticatedServiceUri);
                }
            }
            else
            {
                if (result.Result is null)
                {
                    Logger.GetPostRecordSucceededButReturnedNullResult(_logger, uri, cid, AuthenticatedOrUnauthenticatedServiceUri);
                }
                else
                {
                    Logger.GetPostRecordFailed(
                        _logger,
                        result.StatusCode,
                        uri,
                        cid,
                        result.AtErrorDetail?.Error,
                        result.AtErrorDetail?.Message,
                        AuthenticatedOrUnauthenticatedServiceUri);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the post record for the specified <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference" /> of the post to return the <see cref="PostRecord"/> for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        public async Task<AtProtoHttpResult<PostRecord>> GetPostRecord(
            StrongReference strongReference,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            return await GetPostRecord(strongReference.Uri, strongReference.Cid, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a <see cref="ProfileRecord"/> for the current user.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public async Task<AtProtoHttpResult<ProfileRecord>> GetProfileRecord(
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            AtUri profileUri = new($"at://{Did}/{CollectionNsid.Profile}/self");

            return await GetRecord<ProfileRecord>(
                profileUri,
                service: Service,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the current user's <see cref="ProfileRecord"/>.
        /// </summary>
        /// <param name="profileRecord">The <see cref="ProfileRecord"/> to update.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        public async Task<AtProtoHttpResult<PutRecordResponse>> UpdateProfileRecord(
            ProfileRecord profileRecord,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profileRecord);
            ArgumentNullException.ThrowIfNull(profileRecord.Value);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            if (profileRecord.Uri.Authority is not Did recordDid)
            {
                throw new ArgumentException("Uri authority is not a DID", nameof(profileRecord));
            }

            if (recordDid != Did)
            {
                throw new ArgumentException("Uri authority does not match the current user", nameof(profileRecord));
            }

            return await PutRecord(
                recordValue: profileRecord.Value,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: CollectionNsid.Profile,
                rKey: "self",
                validate: null,
                swapCommit: null,
                swapRecord: profileRecord.Cid,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
