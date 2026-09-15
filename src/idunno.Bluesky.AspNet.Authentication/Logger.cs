// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using Microsoft.Extensions.Logging;

using idunno.AtProto;

namespace idunno.Bluesky.AspNet.Authentication;

internal static partial class Logger
{
    // Handler logging
    [LoggerMessage(10, LogLevel.Information, "AuthenticationScheme: {AuthenticationScheme} signed in.", EventName = "AuthenticationSchemeSignedIn")]
    public static partial void AuthenticationSchemeSignedIn(this ILogger logger, string authenticationScheme);

    [LoggerMessage(11, LogLevel.Information, "AuthenticationScheme: {AuthenticationScheme} signed out.", EventName = "AuthenticationSchemeSignedOut")]
    public static partial void AuthenticationSchemeSignedOut(this ILogger logger, string authenticationScheme);

    [LoggerMessage(12, LogLevel.Debug, "Principal validation failed, no principal or an invalid principal.")]
    public static partial void PrincipalValidationFailedNoPrincipleOrClaims(this ILogger logger);

    [LoggerMessage(13, LogLevel.Debug, "Missing or invalid DID claim.")]
    public static partial void MissingOrInvalidDidClaim (this ILogger logger);

    [LoggerMessage(14, LogLevel.Debug, "No stored claims for {did}.")]
    public static partial void NoStoredClaimsForDid(this ILogger logger, Did did);

    [LoggerMessage(15, LogLevel.Debug, "Principal did not contain a ClaimsIdentity.")]
    public static partial void PrincipalDidNotContainAClaimsIdentity(this ILogger logger);

    // Manager Logging
    [LoggerMessage(20, LogLevel.Error, "OAuth state could not be prepared.")]
    public static partial void CouldNotPrepareOAuthState(this ILogger logger);

    [LoggerMessage(21, LogLevel.Information, "Overriding returnUri port for localhost client ID to {port} due to Development environment.")]
    public static partial void ReturnUriPortOverridden(this ILogger logger, int port);

    [LoggerMessage(22, LogLevel.Debug, "Request contained an expired correlation cookie.")]
    public static partial void ExpiredCorrelationCookie(this ILogger logger);

    [LoggerMessage(23, LogLevel.Warning, "Exception thrown when unprotecting correlation cookie.")]
    public static partial void ExceptionUnprotectingCorrelationCookie(this ILogger logger, Exception ex);

    [LoggerMessage(27, LogLevel.Debug, "Request did not contain a correlation cookie.")]
    public static partial void MissingCorrelationCookie(this ILogger logger);

    [LoggerMessage(28, LogLevel.Warning, "Correlation cookie was unprotected but its contents could not be parsed.")]
    public static partial void MalformedCorrelationCookie(this ILogger logger);

    [LoggerMessage(24, LogLevel.Warning, "SignIn failed due to missing query string on the request.")]
    public static partial void SignInFailedNoQueryString(this ILogger logger);

    [LoggerMessage(25, LogLevel.Warning, "SignIn failed due to missing correlation state.")]
    public static partial void SignInFailedNoCorrelation(this ILogger logger);

    [LoggerMessage(26, LogLevel.Warning, "SignIn failed as OAuth2 response could not be processed.")]
    public static partial void SignInFailedOAuth2ProcessingFailed(this ILogger logger);

    // Claims Transformation Logging
    [LoggerMessage(40, LogLevel.Debug, "Cached claims for {did} found.")]
    public static partial void TransformerCachedClaimsFound(this ILogger logger, Did did);

    [LoggerMessage(41, LogLevel.Debug, "Profile retrieved for {did}.")]
    public static partial void TransformerGetProfileSucceeded(this ILogger logger, Did did);

    [LoggerMessage(42, LogLevel.Debug, "Cached profile claims for {did}.")]
    public static partial void TransformerCachedClaimsForDid(this ILogger logger, Did did);

    [LoggerMessage(43, LogLevel.Debug, "Failed to get profile for {did}, HTTP status code {httpStatusCode}. {atError} {atErrorMessage}")]
    public static partial void TransformerGetProfileFailed(this ILogger logger, Did did, HttpStatusCode httpStatusCode, string? atError, string? atErrorMessage);

    [LoggerMessage(250, LogLevel.Debug, "Identity added to cache for {did}")]
    public static partial void IdentityAddedToCache(this ILogger logger, Did did);

    [LoggerMessage(251, LogLevel.Debug, "Cached Identity not found for {did}")]
    public static partial void IdentityNotFoundInCache(this ILogger logger, Did did);

    [LoggerMessage(252, LogLevel.Error, "Cached identity for {did} is corrupt.")]
    public static partial void CachedIdentityIsCorrupt(this ILogger logger, Did did, Exception ex);

    [LoggerMessage(255, LogLevel.Warning, "The stored identity for {did} could not be unprotected and has been removed from the store. The data protection key ring may have changed, or its keys may not be persisted between restarts.")]
    public static partial void CachedIdentityCouldNotBeUnprotected(this ILogger logger, Did did, Exception ex);

    [LoggerMessage(256, LogLevel.Warning, "The DID claim in the authentication cookie is not a valid DID.")]
    public static partial void InvalidDidInCookie(this ILogger logger);

    [LoggerMessage(257, LogLevel.Warning, "The in memory identity store reached its size limit of {sizeLimit} identities and has evicted some of them. The users whose identities were evicted will be signed out even though their authentication cookies are still valid. Use a distributed identity store, or raise the size limit.")]
    public static partial void EphemeralIdentityStoreCapacityReached(this ILogger logger, int sizeLimit);

    [LoggerMessage(258, LogLevel.Error, "The identity for {did} could not be stored. The in memory identity store is full at its size limit of {sizeLimit} identities and could not make room for it.")]
    public static partial void IdentityCouldNotBeStored(this ILogger logger, Did did, int sizeLimit);

    [LoggerMessage(259, LogLevel.Warning, "The in memory correlation state cache reached its size limit of {sizeLimit} entries and has evicted some of them. Logins in flight when their state was evicted will fail at the callback. Use a distributed correlation state cache, or raise the size limit.")]
    public static partial void EphemeralCorrelationStateCacheCapacityReached(this ILogger logger, int sizeLimit);

    [LoggerMessage(253, LogLevel.Debug, "Identity cache updated for {did}")]
    public static partial void CachedIdentityUpdated(this ILogger logger, Did did);

    [LoggerMessage(254, LogLevel.Debug, "Entry for {did} removed from cache")]
    public static partial void CachedIdentityRemoved(this ILogger logger, Did did);

    [LoggerMessage(260, LogLevel.Debug, "StartRefresh entered for {did}")]
    public static partial void StartRefreshEntered(this ILogger logger, Did did);

    [LoggerMessage(261, LogLevel.Debug, "StartRefresh denied for {did}, refresh already in progress.")]
    public static partial void StartRefreshDenied(this ILogger logger, Did did);

    [LoggerMessage(262, LogLevel.Debug, "EndRefresh finished for {did}")]
    public static partial void EndRefreshFinished(this ILogger logger, Did did);

    [LoggerMessage(263, LogLevel.Error, "Token refresh failed for {did}.")]
    public static partial void TokenRefreshThrew(this ILogger logger, Did did, Exception ex);

    [LoggerMessage(264, LogLevel.Warning, "EndRefresh for {did} did not release the refresh lock as it is now held by another caller.")]
    public static partial void EndRefreshLockNotOwned(this ILogger logger, Did did);

    [LoggerMessage(265, LogLevel.Warning, "Token refresh failed for {did}, but the identity store holds unexpired credentials from a concurrent refresh, which will be used instead.")]
    public static partial void TokenRefreshFailedButStoreIsCurrent(this ILogger logger, Did did);

    [LoggerMessage(266, LogLevel.Debug, "Credentials for {did} were revoked at the authorization server during sign out.")]
    public static partial void CredentialsRevokedOnSignOut(this ILogger logger, Did did);

    [LoggerMessage(267, LogLevel.Warning, "Credentials for {did} could not be revoked at the authorization server during sign out. The local sign out completed, but the tokens remain valid until they expire.")]
    public static partial void CredentialRevocationFailed(this ILogger logger, Did did, Exception exception);

    [LoggerMessage(268, LogLevel.Debug, "Sign out was called without the request having been authenticated. The DID was recovered from the request cookie.")]
    public static partial void SignOutDidRecoveredFromCookie(this ILogger logger);

    [LoggerMessage(269, LogLevel.Warning, "The identity store time to live for the '{scheme}' authentication scheme is {identityStoreEntryTimeToLive}, which is shorter than the ExpireTimeSpan of {expireTimeSpan}. Users will be signed out when their stored credentials expire, before their authentication cookie does.")]
    public static partial void IdentityStoreTimeToLiveShorterThanCookieLifetime(this ILogger logger, string scheme, TimeSpan identityStoreEntryTimeToLive, TimeSpan expireTimeSpan);

    [LoggerMessage(300, LogLevel.Error, "Credentials were refreshed for {did} but were not DPoPAccessCredentials.")]
    public static partial void CredentialsRefreshedNotDPoP(this ILogger logger, Did did);

    [LoggerMessage(500, LogLevel.Warning, "Using an in-memory cache which is not suitable for production environments. A maximum of 1024 identities will be cached. Identities will not be persisted to storage.")]
    public static partial void UsingInMemoryCacheWarning(this ILogger logger);

    [LoggerMessage(501, LogLevel.Warning, "Using an in-memory cache which is not suitable for production environments. A maximum of 1024 correlation states will be cached. States will not be persisted to storage.")]
    public static partial void UsingInMemoryCorrelationCacheWarning(this ILogger logger);

    [LoggerMessage(502, LogLevel.Warning, "Using an in-memory cache which is not suitable for production environments. A maximum of 1024 profile entries will be cached. Profiles will not be persisted to storage.")]
    public static partial void UsingInMemoryProfileCacheWarning(this ILogger logger);

}
