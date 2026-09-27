// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Implements asp.net claims transformation for ATProto principals, which supplements an ATProto principal
/// with claims derived from the user's Bluesky profile.
/// </summary>
/// <remarks>
/// <para>This transformer requires the an access token issued with transition:generic scope.</para>
/// </remarks>
public sealed class BlueskyClaimsTransformer : IClaimsTransformation
{
    private readonly BlueskyAuthenticationMetrics _metrics;

    private readonly IHttpClientFactory? _httpClientFactory;

    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// The type of the claim added to mark a principal as having already had its profile claims applied.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Claims transformation can run more than once for a request, so the transformer must be able to recognise a
    ///   principal it has already supplemented. Every profile claim is only added when the profile actually carries a
    ///   value for it, so none of them can be used for that, and this marker is added whenever the transformation
    ///   succeeds regardless of what the profile contained.
    /// </para>
    /// </remarks>
    public const string ProfileClaimsAppliedClaimType = "urn:bluesky:aspnet:profileclaimsapplied";

    /// <summary>
    /// Create a new instance of <see cref="BlueskyClaimsTransformer"/>
    /// </summary>
    /// <param name="loggerFactory">The <see cref="LoggerFactory"/> to create loggers from.</param>
    /// <param name="options">The <see cref="BlueskyClaimsTransformerOptions"/> to configure the transformer.</param>
    /// <param name="blueskyAgentOptions">The <see cref="Bluesky.BlueskyAgentOptions"/> to use for the agent retrieving the profile.</param>
    /// <param name="blueskyAuthenticationOptions">
    ///   The <see cref="BlueskyAuthenticationOptions"/> used to locate the <see cref="IIdentityStore"/> any
    ///   credentials updated whilst retrieving the profile should be saved to.
    /// </param>
    /// <param name="meterFactory">The <see cref="IMeterFactory"/> to use for creating the underlying <see cref="Meter"/>.</param>
    /// <param name="httpClientFactory">
    ///   The <see cref="IHttpClientFactory"/> the agent which retrieves the profile should make its requests through.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if <paramref name="options"/>, <paramref name="blueskyAgentOptions"/> or
    ///   <paramref name="blueskyAuthenticationOptions"/> is <see langword="null"/>.
    /// </exception>
    public BlueskyClaimsTransformer(
        ILoggerFactory loggerFactory,
        IOptionsMonitor<BlueskyClaimsTransformerOptions> options,
        IOptionsMonitor<BlueskyAgentOptions> blueskyAgentOptions,
        IOptionsMonitor<BlueskyAuthenticationOptions> blueskyAuthenticationOptions,
        IMeterFactory? meterFactory = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(blueskyAgentOptions);
        ArgumentNullException.ThrowIfNull(blueskyAuthenticationOptions);

        Options = options;

        loggerFactory ??= NullLoggerFactory.Instance;

        _loggerFactory = loggerFactory;

        BlueskyAgentOptions = blueskyAgentOptions;
        AuthenticationOptions = blueskyAuthenticationOptions;
        Logger = loggerFactory.CreateLogger(GetType().FullName!);

        _httpClientFactory = httpClientFactory;
        _metrics = new BlueskyAuthenticationMetrics(meterFactory);
    }

    private IOptionsMonitor<BlueskyAgentOptions> BlueskyAgentOptions { get; }

    private IOptionsMonitor<BlueskyAuthenticationOptions> AuthenticationOptions { get; }

    [NotNull]
    private IOptionsMonitor<BlueskyClaimsTransformerOptions> Options { get; }

    private IProfileCache Cache
    {
        get
        {
            if (Options.CurrentValue.Cache is null)
            {
                throw new InvalidOperationException("Profile cache is not configured.");
            }

            return Options.CurrentValue.Cache;
        }
    }

    private ILogger Logger { get; }

    /// <summary>
    /// Provides a central transformation point to change the specified principal.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> to transform.</param>
    /// <returns>The transformed <see cref="ClaimsPrincipal"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="principal"/> is <see langword="null" />.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions calls.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions calls.")]
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is null ||
            principal.HasClaim(claim => claim.Type == ProfileClaimsAppliedClaimType))
        {
            return principal;
        }

        if (!AtProtoCredential.TryCreate(principal, out DPoPAccessCredentials? dPoPAccessCredentials) || dPoPAccessCredentials is null)
        {
            return principal;
        }

        // Matches the agent's own IsAuthenticated check, made here so that a principal whose credentials have expired
        // costs nothing, and so the cache can be consulted before an agent is built.
        if (dPoPAccessCredentials.ExpiresOn <= DateTimeOffset.UtcNow)
        {
            return principal;
        }

        Did did = dPoPAccessCredentials.Did;

        // Checked before the agent is created, because constructing one is not free and the overwhelming majority of
        // requests from a signed in user hit the cache.
        ProfileCacheEntry? cachedProfile = await Cache.GetCachedValue(did).ConfigureAwait(false);

        if (cachedProfile is not null)
        {
            Logger.TransformerCachedClaimsFound(did);
            _metrics.ProfileCacheHits.Add(1);
            return SupplementClaimsPrincipal(principal, cachedProfile);
        }

        _metrics.ProfileCacheMisses.Add(1);

        using (BlueskyAgent agent = CreateAgent(principal))
        {
            // The agent makes authenticated calls, so its credentials can be updated underneath us, most commonly
            // by a DPoP nonce rotation. Without this any updated credentials would be discarded when the agent is
            // disposed and the next call would have to pay for another nonce rotation round trip.
            IIdentityStore? identityStore = ResolveIdentityStore(principal.Identity.AuthenticationType);

            if (identityStore is not null)
            {
                agent.CredentialsUpdatedAsync = identityStore.OnCredentialsUpdated;
            }

            if (agent.IsAuthenticated)
            {
                AtProtoHttpResult<ProfileViewDetailed> getProfileResult = await agent.GetProfile(agent.Did).ConfigureAwait(false);

                if (getProfileResult.Succeeded)
                {
                    Logger.TransformerGetProfileSucceeded(agent.Did);
                    cachedProfile = new (getProfileResult.Result, agent.Service.ToString());

                    // The profile, and so the handle in it, comes from the user's own PDS. A handle claim is used by
                    // applications to name and to authorize, so a handle which does not resolve back to the DID it was
                    // returned for is dropped rather than presented as though the directory agreed with it.
                    if (Options.CurrentValue.VerifyHandle &&
                        cachedProfile.Handle is not null &&
                        !await Resolution.VerifyHandle(
                            cachedProfile.Handle,
                            agent.Did,
                            loggerFactory: _loggerFactory,
                            httpClient: agent.HttpClient,
                            cancellationToken: CancellationToken.None).ConfigureAwait(false))
                    {
                        Logger.HandleVerificationFailed(cachedProfile.Handle.ToString(), agent.Did);
                        _metrics.HandleVerificationFailures.Add(1);
                        cachedProfile = cachedProfile with { Handle = null };
                    }

                    await Cache.Add(agent.Did, cachedProfile).ConfigureAwait(false);
                    Logger.TransformerCachedClaimsForDid(agent.Did);
                    return SupplementClaimsPrincipal(principal, cachedProfile);
                }
                else
                {
                    Logger.TransformerGetProfileFailed(agent.Did, getProfileResult.StatusCode, getProfileResult.AtErrorDetail?.Error, getProfileResult.AtErrorDetail?.Message);
                    return principal;
                }
            }
        }

        return principal;
    }

    /// <summary>
    /// Creates the <see cref="BlueskyAgent"/> the profile is retrieved with.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> whose credentials the agent should use.</param>
    /// <remarks>
    /// <para>
    ///   An agent created without an <see cref="IHttpClientFactory"/> builds a service provider, and so a connection
    ///   pool, of its own. Claims transformation runs per request, so one is used when the application registered one.
    /// </para>
    /// </remarks>
    private BlueskyAgent CreateAgent(ClaimsPrincipal principal) =>
        _httpClientFactory is null
            ? new BlueskyAgent(principal, BlueskyAgentOptions?.CurrentValue)
            : new BlueskyAgent(principal, _httpClientFactory, BlueskyAgentOptions?.CurrentValue);

    /// <summary>
    /// Resolves the <see cref="IIdentityStore"/> configured for the authentication scheme the principal was issued by.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   <see cref="BlueskyAuthenticationOptions"/> are configured against the name of the authentication scheme they
    ///   belong to, so the unnamed options instance is not necessarily the one the handler is using. The authentication
    ///   type on a principal issued by the handler is the scheme name, so use that to get the right options instance.
    /// </para>
    /// </remarks>
    /// <param name="authenticationScheme">The name of the authentication scheme the principal was issued by, if any.</param>
    private IIdentityStore? ResolveIdentityStore(string? authenticationScheme)
    {
        authenticationScheme = string.IsNullOrEmpty(authenticationScheme) ? BlueskyAuthenticationDefaults.AuthenticationScheme : authenticationScheme;

        return AuthenticationOptions.Get(authenticationScheme).IdentityStore;
    }

    private static ClaimsPrincipal SupplementClaimsPrincipal(ClaimsPrincipal principal, ProfileCacheEntry profile)
    {
        ClaimsIdentity identity = new(principal.Claims, principal.Identity!.AuthenticationType);

        if (profile is not null)
        {
            // Added whatever the profile held, so a principal whose profile has no display name, or no profile values
            // at all, is still recognised as transformed and is not supplemented again.
            identity.AddClaim(new Claim(
                ProfileClaimsAppliedClaimType,
                "true",
                ClaimValueTypes.Boolean,
                profile.Issuer));

            if (profile.Handle is not null)
            {
                string handle = profile.Handle.ToString();

                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Handle,
                    handle,
                    ClaimValueTypes.String,
                    profile.Issuer));

                identity.AddClaim(new Claim(
                    System.Security.Claims.ClaimTypes.Name,
                    handle,
                    ClaimValueTypes.String,
                    profile.Issuer));
            }

            if (!string.IsNullOrEmpty(profile.DisplayName))
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.DisplayName,
                    profile.DisplayName,
                    ClaimValueTypes.String,
                    profile.Issuer));
            }

            if (!string.IsNullOrEmpty(profile.Description))
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Description,
                    profile.Description,
                    ClaimValueTypes.String,
                    profile.Issuer));
            }

            if (!string.IsNullOrEmpty(profile.Pronouns))
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Pronouns,
                    profile.Pronouns,
                    ClaimValueTypes.String,
                    profile.Issuer));
            }

            if (ClaimsExtensions.IsSafeWebUri(profile.Website))
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Website,
                    profile.Website!.ToString(),
                    ClaimValueTypes.String,
                    profile.Issuer));
            }

            if (ClaimsExtensions.IsSafeWebUri(profile.Avatar))
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Avatar,
                    profile.Avatar!.ToString(),
                    ClaimValueTypes.String,
                    profile.Issuer));
            }

            if (ClaimsExtensions.IsSafeWebUri(profile.Banner))
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Banner,
                    profile.Banner!.ToString(),
                    ClaimValueTypes.String,
                    profile.Issuer));
            }
        }

        return new ClaimsPrincipal(identity);
    }
}
