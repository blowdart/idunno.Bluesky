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

    [LoggerMessage(252, LogLevel.Error, "Cached identity {did} is corrupt.")]
    public static partial void CachedIdentityIsCorrupt(this ILogger logger, Did did, Exception ex);

    [LoggerMessage(253, LogLevel.Debug, "Identity cache renewed for {did}")]
    public static partial void CachedIdentityRenewed(this ILogger logger, Did did);

    [LoggerMessage(254, LogLevel.Debug, "Entry for {did} removed from cache")]
    public static partial void CachedIdentityRemoved(this ILogger logger, Did did);

    [LoggerMessage(300, LogLevel.Error, "Credentials were refreshed for {did} but were not DPoPAccessCredentials.")]
    public static partial void CredentialsRefreshedNotDPoP(this ILogger logger, Did did);

    [LoggerMessage(500, LogLevel.Warning, "Using an in-memory cache which is not suitable for production environments. A maximum of 1024 identities will be cached. Identities will not be persisted to storage.")]
    public static partial void UsingInMemoryCacheWarning(this ILogger logger);

    [LoggerMessage(501, LogLevel.Warning, "Using an in-memory cache which is not suitable for production environments. A maximum of 1024 correlation states will be cached. States will not be persisted to storage.")]
    public static partial void UsingInMemoryCorrelationCacheWarning(this ILogger logger);

    [LoggerMessage(502, LogLevel.Warning, "Using an in-memory cache which is not suitable for production environments. A maximum of 1024 profile entries will be cached. Profiles will not be persisted to storage.")]
    public static partial void UsingInMemoryProfileCacheWarning(this ILogger logger);
}
