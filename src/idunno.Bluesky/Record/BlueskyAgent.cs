// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Record;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Creates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <typeparam name="TRecord">The type of record to create.</typeparam>
        /// <param name="record"><para>The record to be created.</para></param>
        /// <param name="collection"><para>The collection the record should be created in.</para></param>
        /// <param name="rKey"><para>An optional <see cref="RecordKey"/> to create the record with.</para></param>
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
        /// <exception cref="ArgumentNullException"><para>Thrown when <paramref name="record"/> or <paramref name="collection"/> is null.</para></exception>
        /// <exception cref="AuthenticationRequiredException"><para>Thrown when the current agent is not authenticated.</para></exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public async Task<AtProtoHttpResult<CreateRecordResult>> CreateBlueskyRecord<TRecord>(
            TRecord record,
            Nsid collection,
            RecordKey? rKey = null,
            bool? validate = true,
            Cid? swapCommit = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : BlueskyRecord
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<CreateRecordResult> result = await CreateRecord<BlueskyRecord>(
                record: record,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: collection,
                rKey: rKey,
                validate: validate,
                swapCommit: swapCommit,
                serviceProxy: serviceProxy,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Gets the Bluesky record specified by the identifying parameters.
        /// </summary>
        /// <typeparam name="TRecord">The type of record to get.</typeparam>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the record from.</param>
        /// <param name="collection">The NSID of the collection the record should be retrieved from.</param>
        /// <param name="rKey">The record key, identifying the record to be retrieved.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/>, <paramref name="collection"/> is null or empty.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<TRecord>>> GetBlueskyRecord<TRecord>(
            AtIdentifier repo,
            Nsid collection,
            RecordKey rKey,
            Cid? cid = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : BlueskyRecord
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(rKey);

            return await GetRecord<TRecord>(
                repo: repo,
                collection: collection,
                rKey: rKey,
                cid: cid,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                serviceProxy: serviceProxy,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the Bluesky record specified by the <paramref name="uri"/>.
        /// </summary>
        /// <typeparam name="TRecord">The type of record to get.</typeparam>
        /// <param name="uri">The <see cref="AtUri"/> of the record to retrieve.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, or its collection or rkey property is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<TRecord>>> GetBlueskyRecord<TRecord>(
            AtUri uri,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : BlueskyRecord
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentNullException.ThrowIfNull(uri.RecordKey);

            return await GetBlueskyRecord<TRecord>(
                repo: uri.Repo,
                collection: uri.Collection,
                rKey: uri.RecordKey,
                cid: null,
                serviceProxy: serviceProxy,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the post record for the specified <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri" /> of the post to return the <see cref="AtProtoRepositoryRecord{Post}"/> for.</param>
        /// <param name="cid">An optional <see cref="Cid" /> of the post to return the <see cref="AtProtoRepositoryRecord{Post}"/> for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Post>>> GetPostRecord(
            AtUri uri,
            Cid? cid = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            AtProtoHttpResult<AtProtoRepositoryRecord<Post>> result = await BlueskyServer.GetPostRecord(
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
        /// <param name="strongReference">The <see cref="StrongReference" /> of the post to return the <see cref="AtProtoRepositoryRecord{Post}"/> for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Post>>> GetPostRecord(
            StrongReference strongReference,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            return await GetPostRecord(strongReference.Uri, strongReference.Cid, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a <see cref="Profile"/> for the current user.
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
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Profile>>> GetProfileRecord(
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            AtUri profileUri = new($"at://{Did}/{CollectionNsid.Profile}/self");

            return await GetRecord<Profile>(
                profileUri,
                service: Service,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the current user's <see cref="Profile"/>.
        /// </summary>
        /// <param name="profile">The <see cref="Profile"/> to create.</param>
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
        public async Task<AtProtoHttpResult<CreateRecordResult>> CreateProfile(
            Profile profile,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await CreateBlueskyRecord(
                record: profile,
                collection: CollectionNsid.Profile,
                rKey: "self",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates or updates the current users profile.
        /// </summary>
        /// <param name="profile">The profile to create from or update to</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<PutRecordResult>> SetProfile(
            Profile profile,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await UpdateProfile(profile, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the current users profile.
        /// </summary>
        /// <param name="profile">The profile update to</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<PutRecordResult>> SetProfile(
            AtProtoRepositoryRecord<Profile> profile,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(profile.Value);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await UpdateProfile(profile, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Update the current user's <see cref="Profile"/>.
        /// </summary>
        /// <param name="profile">The <see cref="AtProtoRepositoryRecord{Profile}"/> to update.</param>
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
        public async Task<AtProtoHttpResult<PutRecordResult>> UpdateProfile(
            AtProtoRepositoryRecord<Profile> profile,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(profile.Value);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            if (profile.Uri.Authority is not Did recordDid)
            {
                throw new ArgumentException("Uri authority is not a DID", nameof(profile));
            }

            if (recordDid != Did)
            {
                throw new ArgumentException("Uri authority does not match the current user", nameof(profile));
            }

            return await PutRecord(
                record: profile.Value,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: CollectionNsid.Profile,
                rKey: "self",
                validate: null,
                swapCommit: null,
                swapRecord: profile.Cid,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Update the current user's <see cref="Profile"/>.
        /// </summary>
        /// <param name="profile">The <see cref="Profile"/> to update with.</param>
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
        public async Task<AtProtoHttpResult<PutRecordResult>> UpdateProfile(
            Profile profile,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await PutRecord(
                record: profile,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: CollectionNsid.Profile,
                rKey: "self",
                validate: null,
                swapCommit: null,
                swapRecord: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a <see cref="BlueskyList"/>.
        /// </summary>
        /// <param name="list">The <see cref="BlueskyList"/> to create.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        public async Task<AtProtoHttpResult<CreateRecordResult>> CreateList(
            BlueskyList list,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(list);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await CreateRecord<BlueskyTimestampedRecord>(
                record: list,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: CollectionNsid.List,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the list referenced by its <paramref name="uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the list to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or its collection property is null .</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="uri"/> does not point to a list.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteList(
            AtUri uri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.List);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await DeleteRecord(
                uri: uri,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the referenced list record.
        /// </summary>
        /// <param name="list">The <see cref="AtProtoRepositoryRecord{TRecord}"/> referenced <see cref="BlueskyList"/> to update.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/> or its Uri, or the URI Collection or RecordKey property is null .</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="list"/> does not point to a list.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<PutRecordResult>> UpdateList(
            AtProtoRepositoryRecord<BlueskyList> list,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(list.Uri);
            ArgumentNullException.ThrowIfNull(list.Uri.Collection);
            ArgumentNullException.ThrowIfNull(list.Uri.RecordKey);
            ArgumentOutOfRangeException.ThrowIfNotEqual(list.Uri.Collection, CollectionNsid.List);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await UpdateList(
                uri: list.Uri,
                list: list.Value,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the list record referenced by its <paramref name="uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the list record to update.</param>
        /// <param name="list">The <see cref="BlueskyList"/> to update the record with</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or its Collection or RecordKey property is null .</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="uri"/> does not point to a list.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        public async Task<AtProtoHttpResult<PutRecordResult>> UpdateList(
            AtUri uri,
            BlueskyList list,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentNullException.ThrowIfNull(uri.RecordKey);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.List);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await PutRecord<BlueskyTimestampedRecord>(
                record: list,
                collection: CollectionNsid.List,
                rKey: uri.RecordKey,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                validate: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds the <paramref name="did"/> to the specified <paramref name="uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the list to add the <paramref name="did"/> to.</param>
        /// <param name="did">The <see cref="Did"/> of the actor to add to the list.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="did"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        public async Task<AtProtoHttpResult<CreateRecordResult>> AddToList(
            AtUri uri,
            Did did,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(did);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            BlueskyListItem listItem = new() { List = uri, Subject = did };

            return await CreateRecord<BlueskyTimestampedRecord>(
                record: listItem,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: CollectionNsid.ListItem,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds the <paramref name="handle"/> to the specified <paramref name="uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the list to add the <paramref name="handle"/> to.</param>
        /// <param name="handle">The <see cref="Did"/> of the actor to add to the list.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="handle"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        public async Task<AtProtoHttpResult<CreateRecordResult>> AddToList(
            AtUri uri,
            Handle handle,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(handle);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            Did? did = await ResolveHandle(handle, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (did is null)
            {
                return new AtProtoHttpResult<CreateRecordResult>(
                    result: null,
                    statusCode: HttpStatusCode.NotFound,
                    httpResponseHeaders: null,
                    atErrorDetail: new AtErrorDetail("NotFound", $"{handle} cannot be resolved"),
                    rateLimit: null);
            }

            return await AddToList(
                uri: uri,
                did: did,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the list entry referred to by the <paramref name="uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or its collection property is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to the list item collection.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        /// <remarks>
        ///     <para>To get a <see cref="AtUri"/> for a particular subject in a list call
        ///           <see cref="GetList(AtUri, int?, string?, IEnumerable{Did}?, CancellationToken)"/>, then, while paging through the results
        ///           search the subject <see cref="Did"/> or <see cref="Handle"/>.
        ///     <example>
        ///      <code>var listEntry = listEntriesResult.Result.FirstOrDefault(listEntry => listEntry.Subject.Handle == "blowdart.me")</code>
        ///      </example>
        ///     </para>
        /// </remarks>
        public async Task<AtProtoHttpResult<Commit>> DeleteFromList(
            AtUri uri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.ListItem);


            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await DeleteRecord(
                uri: uri,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the list entry referred to by the <paramref name="did"/> from the list referred to by <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the list to delete the subject whose <see cref="Did"/> matches <paramref name="did"/>.</param>
        /// <param name="did">The <see cref="Did"/> of the subject to delete from the list</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/>, <paramref name="uri"/> or the uri collection property is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to the list item collection.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        /// <remarks>
        ///    <para>This method iterates through the list members search for the specified <see cref="Did"/>. This may result in multiple API calls
        ///    depending on the size of the list.</para>
        /// </remarks>
        public async Task<AtProtoHttpResult<Commit>> DeleteFromList(
            AtUri uri,
            Did did,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.List);

            ArgumentNullException.ThrowIfNull(did);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<ListViewWithItems> listEntriesResult = await GetList(uri, limit: 100, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!listEntriesResult.Succeeded || listEntriesResult.Result.Count == 0)
            {
                return new AtProtoHttpResult<Commit>(
                    result: null,
                    statusCode: listEntriesResult.StatusCode,
                    httpResponseHeaders: listEntriesResult.HttpResponseHeaders,
                    atErrorDetail: listEntriesResult.AtErrorDetail,
                    rateLimit: listEntriesResult.RateLimit);
            }

            do
            {

                ListItemView? hit = listEntriesResult.Result.FirstOrDefault(listEntry => listEntry.Subject.Did == did);
                if (hit is not null)
                {
                    return await DeleteFromList(hit.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
                }

            }
            while (listEntriesResult.Succeeded &&
                   !string.IsNullOrEmpty(listEntriesResult.Result.Cursor));

            return new AtProtoHttpResult<Commit>(
                result: null,
                statusCode: HttpStatusCode.NotFound,
                httpResponseHeaders: listEntriesResult.HttpResponseHeaders,
                atErrorDetail: new AtErrorDetail("NotFound", $"{did} not found in list {uri}"),
                rateLimit: listEntriesResult.RateLimit);
        }

        /// <summary>
        /// Deletes the list entry referred to by the <paramref name="handle"/> from the list referred to by <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the list to delete the subject whose <see cref="Did"/> matches <paramref name="handle"/>.</param>
        /// <param name="handle">The <see cref="Handle"/> of the subject to delete from the list</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/>, <paramref name="uri"/> or the uri collection property is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to the list item collection.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        /// <remarks>
        ///    <para>This method iterates through the list members search for the specified <see cref="Did"/>. This may result in multiple API calls
        ///    depending on the size of the list.</para>
        /// </remarks>
        public async Task<AtProtoHttpResult<Commit>> DeleteFromList(
            AtUri uri,
            Handle handle,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.List);

            ArgumentNullException.ThrowIfNull(handle);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<ListViewWithItems> listEntriesResult = await GetList(uri, limit: 100, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!listEntriesResult.Succeeded || listEntriesResult.Result.Count == 0)
            {
                return new AtProtoHttpResult<Commit>(
                    result: null,
                    statusCode: listEntriesResult.StatusCode,
                    httpResponseHeaders: listEntriesResult.HttpResponseHeaders,
                    atErrorDetail: listEntriesResult.AtErrorDetail,
                    rateLimit: listEntriesResult.RateLimit);
            }

            do
            {

                ListItemView? hit = listEntriesResult.Result.FirstOrDefault(listEntry => listEntry.Subject.Handle == handle);
                if (hit is not null)
                {
                    return await DeleteFromList(hit.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
                }

            }
            while (listEntriesResult.Succeeded &&
                   !string.IsNullOrEmpty(listEntriesResult.Result.Cursor));

            return new AtProtoHttpResult<Commit>(
                result: null,
                statusCode: System.Net.HttpStatusCode.NotFound,
                httpResponseHeaders: listEntriesResult.HttpResponseHeaders,
                atErrorDetail: new AtErrorDetail("NotFound", $"{handle} not found in list {uri}"),
                rateLimit: listEntriesResult.RateLimit);
        }
    }
}
