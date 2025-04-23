// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Record;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Creates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <typeparam name="TRecordValue">The type of record to create.</typeparam>
        /// <param name="recordValue"><para>The record to be created.</para></param>
        /// <param name="collection"><para>The collection the record should be created in.</para></param>
        /// <param name="rkey"><para>An optional <see cref="RecordKey"/> to create the record with.</para></param>
        /// <param name="validate">
        ///   <para>Gets a flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="swapCommit"><para>Compare and swap with the previous commit by CID.</para></param>
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns><para>The task object representing the asynchronous operation.</para></returns>
        /// <exception cref="ArgumentNullException"><para>Thrown when <paramref name="recordValue"/> or <paramref name="collection"/> is null.</para></exception>
        /// <exception cref="AuthenticationRequiredException"><para>Thrown when the current agent is not authenticated.</para></exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> CreateBlueskyRecord<TRecordValue>(
            TRecordValue recordValue,
            Nsid collection,
            RecordKey? rkey = null,
            bool? validate = true,
            Cid? swapCommit = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecordValue : BlueskyRecordValue
        {
            ArgumentNullException.ThrowIfNull(recordValue);
            ArgumentNullException.ThrowIfNull(collection);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<CreateRecordResult> result = await CreateRecord<BlueskyRecordValue>(
                recordValue: recordValue,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: collection,
                rKey: rkey,
                validate: validate,
                swapCommit: swapCommit,
                serviceProxy: serviceProxy,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return result;
        }

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
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
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
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        public async Task<AtProtoHttpResult<PutRecordResult>> UpdateProfileRecord(
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
