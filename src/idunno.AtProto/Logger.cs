// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using Microsoft.Extensions.Logging;

using idunno.AtProto.Repo;

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

        [LoggerMessage(6, LogLevel.Debug, "CreateSession with GetSession succeeded with access token for {did} on {service}")]
        internal static partial void SessionCreatedFromOAuthLogin(ILogger logger, string did, Uri service);

        [LoggerMessage(7, LogLevel.Error, "CreateSession with GetSession failed with access token for {did} on {service}, {statusCode} {error} {message}")]
        internal static partial void SessionCreatedFromOAuthLoginFailed(ILogger logger, string did, Uri service, HttpStatusCode statusCode, string? error, string? message);

        [LoggerMessage(8, LogLevel.Debug, "Login() called with OAuthCredentials for {did} on {service}.")]
        internal static partial void AgentAuthenticatedWithOAuthCredentials(ILogger logger, string did, Uri service);

        [LoggerMessage(9, LogLevel.Debug, "Login() called with DPoP bound OAuthCredentials for {did} on {service}.")]
        internal static partial void AgentAuthenticatedWithDPoPOAuthCredentials(ILogger logger, string did, Uri service);

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

        [LoggerMessage(23, LogLevel.Error, "StartTokenRefreshTime() called but token refresh is disabled")]
        internal static partial void TokenRefreshTimerStartCalledButRefreshDisabled (ILogger logger);

        [LoggerMessage(24, LogLevel.Error, "RefreshToken API call threw")]
        internal static partial void TokenRefreshApiThrew(ILogger logger, Exception e);

        // Restore Session logging
        [LoggerMessage(30, LogLevel.Debug, "RestoreSession called for {did} on {service}")]
        internal static partial void RestoreSessionCalled(ILogger logger, Did did, Uri service);

        [LoggerMessage(31, LogLevel.Error, "RestoreSession failed DID validation, expected {expected}, found {actual}")]
        internal static partial void RestoreSessionDidValidationFailed(ILogger logger, Did expected, Did actual);

        [LoggerMessage(32, LogLevel.Debug, "RestoreSession succeeded for {did} on {service}")]
        internal static partial void RestoreSessionSucceeded(ILogger logger, Did did, Uri service);

        // Refresh Session logging
        [LoggerMessage(40, LogLevel.Debug, "RefreshSession called on {service} with token #{tokenHash}")]
        internal static partial void RefreshSessionCalled(ILogger logger, Uri service, string tokenHash);

        [LoggerMessage(41, LogLevel.Error, "RefreshSession called without an authenticated session")]
        internal static partial void RefreshSessionFailedNoSession(ILogger logger);

        [LoggerMessage(42, LogLevel.Error, "RefreshSession API failed for #{tokenHash} on {service} with {statusCode}")]
        internal static partial void RefreshSessionApiCallFailed(ILogger logger, Uri service, string tokenHash, HttpStatusCode statusCode);

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
        internal static partial void CreateRecordSucceeded(ILogger logger, AtUri uri, Cid cid, Nsid collection, Uri service);

        [LoggerMessage(91, LogLevel.Error, "CreateRecord on {service} succeeded but the result was null")]
        internal static partial void CreateRecordSucceededButNullResult(ILogger logger, Uri service);

        [LoggerMessage(92, LogLevel.Error, "CreateRecord into {collection} failed with status code {status}, \"{error}\" \"{message}\" on {service}")]
        internal static partial void CreateRecordFailed(ILogger logger, HttpStatusCode status, Nsid collection, string? error, string? message, Uri service);

        [LoggerMessage(93, LogLevel.Error, "CreatedRecord failed as current session is not authenticated.")]
        internal static partial void CreateRecordFailedAsSessionIsAnonymous(ILogger logger);

        [LoggerMessage(100, LogLevel.Error, "DeleteRecord failed as current session is not authenticated.")]
        internal static partial void DeleteRecordFailedAsSessionIsAnonymous(ILogger logger);

        [LoggerMessage(101, LogLevel.Debug, "DeleteRecord succeeded, deleted {repo} {collection} {rKey} on {service}. Commit: {commit}")]
        internal static partial void DeleteRecordSucceeded(ILogger logger, AtIdentifier repo, Nsid collection, RecordKey rKey, Uri service, Commit commit);

        [LoggerMessage(102, LogLevel.Error, "DeleteRecord failed with {statusCode} / {error} {message} against {repo} {collection} {rKey} on {service}.")]
        internal static partial void DeleteRecordFailed(ILogger logger, HttpStatusCode statusCode, string? error, string? message, AtIdentifier repo, Nsid collection, RecordKey rKey, Uri service);

        [LoggerMessage(110, LogLevel.Debug, "GetRecord {repo} {collection} {rKey} on {service}.")]
        internal static partial void GetRecordCalled(ILogger logger, AtIdentifier repo, Nsid collection, RecordKey rKey, Uri service);

        [LoggerMessage(111, LogLevel.Error, "GetRecord failed with with {statusCode}, \"{error}\" \"{message}\" against {repo} {collection} {rKey} on {service}.")]
        internal static partial void GetRecordFailed(ILogger logger, HttpStatusCode statusCode, AtIdentifier repo, Nsid collection, RecordKey rKey, string? error, string? message, Uri service);

        [LoggerMessage(112, LogLevel.Error, "GetRecord succeeded but returned a null record against {repo} {collection} {rKey} on {service}.")]
        internal static partial void GetRecordSucceededButReturnedNullResult(ILogger logger, AtIdentifier repo, Nsid collection, RecordKey rKey, Uri service);

        [LoggerMessage(120, LogLevel.Debug, "ListRecords {repo} {collection} on {service}.")]
        internal static partial void ListRecordsCalled(ILogger logger, AtIdentifier repo, Nsid collection, Uri service);

        [LoggerMessage(121, LogLevel.Error, "ListRecords failed with with {statusCode}, \"{error}\" \"{message}\" against {repo} {collection} on {service}.")]
        internal static partial void ListRecordsFailed(ILogger logger, HttpStatusCode statusCode, AtIdentifier repo, Nsid collection, string? error, string? message, Uri service);

        [LoggerMessage(122, LogLevel.Error, "ListRecords succeeded but returned a null result against {repo} {collection} on {service}.")]
        internal static partial void ListRecordsSucceededButReturnedNullResult(ILogger logger, AtIdentifier repo, Nsid collection, Uri service);

        [LoggerMessage(130, LogLevel.Error, "UploadBlob to {service} failed as current session is not authenticated.")]
        internal static partial void UploadBlobFailedAsSessionIsAnonymous(ILogger logger, Uri service);

        [LoggerMessage(131, LogLevel.Error, "UploadBlob to {service} failed as blob is length is zero.")]
        internal static partial void UploadBlobFailedAsBlobLengthIsZero(ILogger logger, Uri service);

        [LoggerMessage(132, LogLevel.Error, "UploadBlob to {service} threw.")]
        internal static partial void UploadBlobThrewHttpRequestException(ILogger logger, Uri service, Exception ex);

        [LoggerMessage(150, LogLevel.Debug, "ApplyWrites succeeded, commit id {cid}, revision {revision}  on {service}")]
        internal static partial void ApplyWritesSucceeded(ILogger logger, Cid cid, string revision, Uri service);

        [LoggerMessage(151, LogLevel.Error, "ApplyWrites failed as current session is not authenticated.")]
        internal static partial void ApplyWritesFailedAsSessionIsAnonymous(ILogger logger);

        [LoggerMessage(152, LogLevel.Error, "ApplyWrites failed with status code {statusCode}, Error:\"{error}\" Message:\"{message}\" on {service}.")]
        internal static partial void ApplyWritesApiCallFailed(ILogger logger, HttpStatusCode statusCode, string? error, string? message, Uri service);

        [LoggerMessage(160, LogLevel.Debug, "Put succeeded, updated or created {uri} {cid} in {collection} on {service}")]
        internal static partial void PutRecordSucceeded(ILogger logger, AtUri uri, Cid cid, Nsid collection, Uri service);

        [LoggerMessage(161, LogLevel.Error, "PutRecord on {service} succeeded but the result was null")]
        internal static partial void PutRecordSucceededButNullResult(ILogger logger, Uri service);

        [LoggerMessage(162, LogLevel.Error, "PutRecord into {collection} with {recordKey}, failed with status code {status}, \"{error}\" \"{message}\" on {service}")]
        internal static partial void PutRecordFailed(ILogger logger, HttpStatusCode status, Nsid collection, RecordKey recordKey, string? error, string? message, Uri service);

        [LoggerMessage(163, LogLevel.Error, "PutRecord failed as current session is not authenticated.")]
        internal static partial void PutRecordFailedAsSessionIsAnonymous(ILogger logger);

        [LoggerMessage(170, LogLevel.Error, "GetServiceAuth failed for {service} as current session is not authenticated.")]
        internal static partial void GetServiceAuthFailedAsSessionIsAnonymous(ILogger logger, Uri service);

        // AtProtoClient logging
        [LoggerMessage(200, LogLevel.Debug, "{method} request to {requestUri} succeeded.")]
        internal static partial void AtProtoClientRequestSucceeded(ILogger logger, Uri requestUri, HttpMethod method);

        [LoggerMessage(201, LogLevel.Error, "{method} request to {requestUri} failed with status code {status}, \"{error}\" \"{message}\"")]
        internal static partial void AtProtoClientRequestFailed(ILogger logger, Uri requestUri, HttpMethod method, HttpStatusCode status, string? error, string? message);

        [LoggerMessage(202, LogLevel.Debug, "{method} request to {requestUri} cancelled.")]
        internal static partial void AtProtoClientRequestCancelled(ILogger logger, Uri requestUri, HttpMethod method);

        [LoggerMessage(203, LogLevel.Debug, "DPoP nonce changed on {method} call to {requestUri}")]
        internal static partial void AtProtoClientDetectedDPoPNonceChanged(ILogger logger, Uri requestUri, HttpMethod method);

        // Service Auth logging
        [LoggerMessage(250, LogLevel.Debug, "Requesting {lxm} service token from {endpoint} for {audience} with a validity of {expires}")]
        internal static partial void RequestingServiceAuthToken(ILogger logger, Uri endpoint, Did audience, string expires, Nsid lxm);

        [LoggerMessage(251, LogLevel.Debug, "Requesting {lxm} service token from {endpoint} for {audience} with no expiry override specified.")]
        internal static partial void RequestingServiceAuthTokenNoExpirySpecified(ILogger logger, Uri endpoint, Did audience, Nsid lxm);

        [LoggerMessage(255, LogLevel.Debug, "Acquired service token for {audience}/{lxm} from {endpoint}, valid for {validity}")]
        internal static partial void ServiceAuthTokenAcquired(ILogger logger, Uri endpoint, Did audience, string validity, Nsid lxm);

        [LoggerMessage(260, LogLevel.Error, "Service token acquisition failed for user {user}, for {audience}/{lxm} from {endpoint} with status code {status}, \"{error}\" \"{message}\" ")]
        internal static partial void ServiceAuthTokenAcquisitionFailed(ILogger logger, Uri endpoint, Did user, Did audience, Nsid lxm, HttpStatusCode status, string? error, string? message);

        [LoggerMessage(300, LogLevel.Debug, "Agent credentials updated via OnCredentialsUpdatedCallBack().")]
        internal static partial void OnCredentialUpdatedCallbackCalled(ILogger logger);

        [LoggerMessage(301, LogLevel.Error, "Agent credentials update via OnCredentialsUpdatedCallBack() ignored, unexpected credentials type.")]
        internal static partial void OnCredentialUpdatedCallbackCalledWithUnexpectedCredentialType(ILogger logger);

        // AtProtoServer logging

        // AtProtoServer Identity Logging
        [LoggerMessage(500, LogLevel.Debug, "Resolving {handle} via DNS, looking for {txtRecord}")]
        internal static partial void ResolvingHandleViaDNS(ILogger logger, Handle handle, string txtRecord);

        [LoggerMessage(501, LogLevel.Debug,"Resolved {handle} via DNS to {did}")]
        internal static partial void ResolvedHandleToDidViaDNS(ILogger logger, Handle handle, Did did);

        [LoggerMessage(502, LogLevel.Debug, "Resolving {handle} via HTTP, requesting {uri}")]
        internal static partial void ResolvingHandleViaHttp(ILogger logger, Handle handle, Uri uri);

        [LoggerMessage(503, LogLevel.Debug, "HTTP resolution request for {handle} returned {response}")]
        internal static partial void HttpHandleResolutionReturned(ILogger logger, Handle handle, string response);

        [LoggerMessage(504, LogLevel.Debug, "Resolved {handle} via HTTP to {did}")]
        internal static partial void ResolvedHandleToDidViaHttp(ILogger logger, Handle handle, Did did);

        [LoggerMessage(505, LogLevel.Error, "HTTP request for {handle} from {Uri} did not parse as a DID")]
        internal static partial void HttpHandleResolutionParseFailed(ILogger logger, Handle handle, Uri uri);

        [LoggerMessage(506, LogLevel.Error, "HTTP request for {handle} to {Uri} failed with HTTP status code of {statusCode}")]
        internal static partial void HttpHandleResolutionRequestFailed(ILogger logger, Handle handle, Uri uri, HttpStatusCode statusCode);

        // AtProtoServer auth logging
        [LoggerMessage(600, LogLevel.Debug, "Generated oauth login {loginUri} for {authority}, correlation {correlation}")]
        internal static partial void OAuthLoginUriGenerated(ILogger logger, Uri authority, Uri loginUri, Guid correlation);

        [LoggerMessage(601, LogLevel.Debug, "OAuth login completed, correlation {correlation}")]
        internal static partial void OAuthLoginCompleted(ILogger logger, Guid correlation);

        [LoggerMessage(602, LogLevel.Error, "OAuth login processing failed {error} {errorDescription}, correlation {correlation}")]
        internal static partial void OAuthLoginFailed(ILogger logger, Guid correlation, string? error, string? errorDescription);

        [LoggerMessage(603, LogLevel.Error, "DPoP header is already present on request to {host}/{path}")]
        internal static partial void DPoPHeaderAlreadyPresent(ILogger logger, string? host, string? path);

        [LoggerMessage(604, LogLevel.Information, "DPoP header added to request to {host}/{path}")]
        internal static partial void DPoPHeaderAddedToTokenRequest(ILogger logger, string? host, string? path);

        [LoggerMessage(605, LogLevel.Error, "OAuth login access token did not contain AtProto in scope, correlation {correlation}")]
        internal static partial void OAuthTokenDoesNotContainAtProtoScope(ILogger logger, Guid correlation);

        [LoggerMessage(606, LogLevel.Error, "OAuth login access token issuer {actual} did not match the expected {expected}, correlation {correlation}")]
        internal static partial void OAuthTokenHasMismatchedAuthority(ILogger logger, Uri expected, Uri actual, Guid correlation);
    }
}
