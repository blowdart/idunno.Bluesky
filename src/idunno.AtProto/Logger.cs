// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto
{
    internal static partial class Logger
    {
        // Create session logging
        [LoggerMessage(1, LogLevel.Debug, "CreateSession called for {did} on {service}")]
        internal static partial void CreateSessionCalled(ILogger logger, string did, Uri service);

        [LoggerMessage(2, LogLevel.Debug, "CreateSession API returned {statusCode}")]
        internal static partial void CreateSessionReturned(ILogger logger, HttpStatusCode statusCode);

        [LoggerMessage(3, LogLevel.Error, "CreateSession JWT validation failed")]
        internal static partial void CreateSessionJwtValidationFailed(ILogger logger);

        [LoggerMessage(4, LogLevel.Error, "CreateSession failed {statusCode}")]
        internal static partial void CreateSessionFailed(ILogger logger, HttpStatusCode statusCode);

        // Delete session logging
        [LoggerMessage(10, LogLevel.Debug, "Logout called for {did} on {service}")]
        internal static partial void LogoutCalled(ILogger logger, Did? did, Uri service);

        [LoggerMessage(11, LogLevel.Debug, "Logout API returned {statusCode} for {did} on {service}")]
        internal static partial void LogoutFailed(ILogger logger, Did? did, Uri service, HttpStatusCode statusCode);

        // Token refresh logging
        [LoggerMessage(20, LogLevel.Debug, "Token refresh timer started")]
        internal static partial void TokenRefreshTimerStarted(ILogger logger);

        [LoggerMessage(21, LogLevel.Debug, "Token refresh timer stopped")]
        internal static partial void TokenRefreshTimerStopped(ILogger logger);

        [LoggerMessage(22, LogLevel.Information, "Background token refresh fired")]
        internal static partial void BackgroundTokenRefreshFired(ILogger logger);

        // Restore Session logging
        [LoggerMessage(30, LogLevel.Debug, "RestoreSession called for {did} on {service}")]
        internal static partial void RestoreSessionCalled(ILogger logger, Did did, Uri service);

        [LoggerMessage(31, LogLevel.Error, "RestoreSession failed DID validation, expected {expected}, found {actual}")]
        internal static partial void RestoreSessionDidValidationFailed(ILogger logger, Did expected, Did actual);

        [LoggerMessage(32, LogLevel.Debug, "RestoreSession succeeded for {did} on {service}")]
        internal static partial void RestoreSessionSucceeded(ILogger logger, Did did, Uri service);

        // Refresh Session logging
        [LoggerMessage(40, LogLevel.Debug, "RefreshSession called for {did} on {service}")]
        internal static partial void RefreshSessionCalled(ILogger logger, Did did, Uri service);

        [LoggerMessage(41, LogLevel.Error, "RefreshSession called on {service} without an authenticated session")]
        internal static partial void RefreshSessionFailedNoSession(ILogger logger, Uri service);

        [LoggerMessage(42, LogLevel.Error, "RefreshSession API failed for {did} on {service} with {statusCode}")]
        internal static partial void RefreshSessionApiCallFailed(ILogger logger, Did did, Uri service, HttpStatusCode statusCode);

        [LoggerMessage(43, LogLevel.Error, "RefreshSession token validation failed for {did} on {service}")]
        internal static partial void RefreshSessionTokenValidationFailed(ILogger logger, Did did, Uri service);

        [LoggerMessage(44, LogLevel.Debug, "RefreshSession succeeded for {did} on {service}")]
        internal static partial void RefreshSessionSucceeded(ILogger logger, Did did, Uri service);

        // Resolution methods logging
        [LoggerMessage(50, LogLevel.Debug, "ResolveHandle called for {handle}")]
        internal static partial void ResolveHandleCalled(ILogger logger, string handle);

        [LoggerMessage(51, LogLevel.Error, "ResolveHandle could not resolve {handle} to a DID")]
        internal static partial void CouldNotResolveHandleToDid(ILogger logger, string handle);

        [LoggerMessage(52, LogLevel.Debug, "ResolveHandle resolved {handle} to {did}")]
        internal static partial void ResolveHandleToDid(ILogger logger, string handle, Did did);

        [LoggerMessage(60, LogLevel.Debug, "ResolveDidDocument called for {did}")]
        internal static partial void ResolveDidDocumentCalled(ILogger logger, Did did);

        [LoggerMessage(61, LogLevel.Error, "ResolveDidDocument failed for {did} {statusCode}")]
        internal static partial void ResolveDidDocumentFailed(ILogger logger, Did did, HttpStatusCode statusCode);

        [LoggerMessage(70, LogLevel.Debug, "ResolvePds called for {did}")]
        internal static partial void ResolvePdsCalled(ILogger logger, Did did);

        [LoggerMessage(71, LogLevel.Error, "ResolvePdsFailed failed for {did}")]
        internal static partial void ResolvePdsFailed(ILogger logger, Did did);

        [LoggerMessage(80, LogLevel.Debug, "ResolveAuthorizationServer called for {pds}")]
        internal static partial void ResolveAuthorizationServerCalled(ILogger logger, Uri pds);

        [LoggerMessage(81, LogLevel.Debug, "ResolveAuthorizationServer resolved {service} for {pds}")]
        internal static partial void ResolveAuthorizationServerDiscovered(ILogger logger, Uri pds, Uri service);

        [LoggerMessage(82, LogLevel.Error, "ResolveAuthorizationServer could not resolve for {pds}")]
        internal static partial void ResolveAuthorizationServerFailed(ILogger logger, Uri pds);

        // Repo Operations logging
        [LoggerMessage(90, LogLevel.Debug, "CreateRecord succeeded, created {uri} {cid} in {collection} on {service}")]
        internal static partial void CreateRecordSucceeded(ILogger logger, AtUri uri, AtCid cid, Nsid collection, Uri service);

        [LoggerMessage(91, LogLevel.Error, "CreateRecord on {service} succeeded but the result was null")]
        internal static partial void CreateRecordSucceededButNullResult(ILogger logger, Uri service);

        [LoggerMessage(92, LogLevel.Error, "CreateRecord into {collection} failed with status code {status}, {error} {message} on {service}")]
        internal static partial void CreateRecordFailed(ILogger logger, HttpStatusCode status, Nsid collection, string? error, string? message, Uri service);

        [LoggerMessage(93, LogLevel.Error, "CreatedRecord failed as current session is not authenticated.")]
        internal static partial void CreateRecordFailedAsSessionIsAnonymous(ILogger logger);

        [LoggerMessage(100, LogLevel.Error, "DeleteRecord failed as current session is not authenticated.")]
        internal static partial void DeleteRecordFailedAsSessionIsAnonymous(ILogger logger);

        [LoggerMessage(101, LogLevel.Debug, "DeleteRecord succeeded, deleted {repo} {collection} {rKey} on {service}.")]
        internal static partial void DeleteRecordSucceeded(ILogger logger, AtIdentifier repo, Nsid collection, RecordKey rKey, Uri service);

        [LoggerMessage(102, LogLevel.Error, "DeleteRecord failed with {statusCode} / {error} {message} against {repo} {collection} {rKey} on {service}.")]
        internal static partial void DeleteRecordFailed(ILogger logger, HttpStatusCode statusCode, string? error, string? message, AtIdentifier repo, Nsid collection, RecordKey rKey, Uri service);

        [LoggerMessage(110, LogLevel.Debug, "GetRecord {repo} {collection} {rKey} on {service}.")]
        internal static partial void GetRecordCalled(ILogger logger, AtIdentifier repo, Nsid collection, RecordKey rKey, Uri service);

        [LoggerMessage(111, LogLevel.Error, "GetRecord failed with with {statusCode} / {error} {message} against {repo} {collection} {rKey} on {service}.")]
        internal static partial void GetRecordFailed(ILogger logger, HttpStatusCode statusCode, AtIdentifier repo, Nsid collection, RecordKey rKey, string? error, string? message, Uri service);

        [LoggerMessage(112, LogLevel.Error, "GetRecord succeeded but returned a null record against {repo} {collection} {rKey} on {service}.")]
        internal static partial void GetRecordSucceededButReturnedNullResult(ILogger logger, AtIdentifier repo, Nsid collection, RecordKey rKey, Uri service);

        [LoggerMessage(120, LogLevel.Debug, "ListRecords {repo} {collection} on {service}.")]
        internal static partial void ListRecordsCalled(ILogger logger, AtIdentifier repo, Nsid collection, Uri service);

        [LoggerMessage(121, LogLevel.Error, "ListRecords failed with with {statusCode} / {error} {message} against {repo} {collection} on {service}.")]
        internal static partial void ListRecordsFailed(ILogger logger, HttpStatusCode statusCode, AtIdentifier repo, Nsid collection, string? error, string? message, Uri service);

        [LoggerMessage(122, LogLevel.Error, "ListRecords succeeded but returned a null result against {repo} {collection} on {service}.")]
        internal static partial void ListRecordsSucceededButReturnedNullResult(ILogger logger, AtIdentifier repo, Nsid collection, Uri service);

        [LoggerMessage(130, LogLevel.Error, "UploadBlob to {service} failed as current session is not authenticated.")]
        internal static partial void UploadBlobFailedAsSessionIsAnonymous(ILogger logger, Uri service);

        [LoggerMessage(131, LogLevel.Error, "UploadBlob to {service} failed as blob is empty.")]
        internal static partial void UploadBlobFailedAsSessionIsEmpty(ILogger logger, Uri service);

        [LoggerMessage(140, LogLevel.Information, "SetTokens called for {did} on {service}")]
        internal static partial void UpdateTokensCalled(ILogger logger, Did did, Uri service);

        [LoggerMessage(141, LogLevel.Error, "SetTokens for {did} on {service} was passed invalid tokens")]
        internal static partial void UpdateTokensGivenInvalidTokens(ILogger logger, Did did, Uri service);
    }
}
