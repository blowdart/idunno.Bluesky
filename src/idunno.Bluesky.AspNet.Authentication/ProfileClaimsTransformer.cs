// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Implements asp.net claims transformation for ATProto principals, which supplements an ATProto principal
/// with claims derived from the user's Bluesky profile.
/// </summary>
/// <remarks>
/// <para>This transformer requires the an access token issued with transition:generic scope.</para>
/// </remarks>
public sealed class ProfileClaimsTransformer: IClaimsTransformation
{
    /// <summary>
    /// Create a new instance of <see cref="ProfileClaimsTransformer"/>
    /// </summary>
    /// <param name="loggerFactory">The <see cref="LoggerFactory"/> to create loggers from.</param>
    /// <param name="options">The <see cref="ProfileClaimsTransformerOptions"/> to configure the transformer.</param>
    /// <param name="blueskyAgentOptions">The <see cref="Bluesky.BlueskyAgentOptions"/> to use for the agent retrieving the profile.</param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown if <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public ProfileClaimsTransformer(
        ILoggerFactory loggerFactory,
        IOptionsMonitor<ProfileClaimsTransformerOptions> options,
        IOptionsMonitor<BlueskyAgentOptions> blueskyAgentOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(blueskyAgentOptions);

        Options = options;

        loggerFactory ??= NullLoggerFactory.Instance;

        BlueskyAgentOptions = blueskyAgentOptions;
        Logger = loggerFactory.CreateLogger(GetType().FullName!);
    }

    private IOptionsMonitor<BlueskyAgentOptions> BlueskyAgentOptions { get; }

    [NotNull]
    private IOptionsMonitor<ProfileClaimsTransformerOptions> Options { get; }

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
            principal.HasClaim(claim => claim.Type == Bluesky.ClaimTypes.DisplayName))
        {
            return principal;
        }

        DPoPAccessCredentials dPoPAccessCredentials = AtProtoCredential.Create(principal);
        if (dPoPAccessCredentials == null)
        {
            return principal;
        }

        using (BlueskyAgent agent = new(principal, BlueskyAgentOptions?.CurrentValue))
        {
            if (agent.IsAuthenticated)
            {
                ProfileCacheEntry? cachedProfile = await Cache.GetCachedValue(agent.Did).ConfigureAwait(false);
                
                if (cachedProfile is not null)
                {
                    Logger.TransformerCachedClaimsFound(agent.Did);
                    return SupplementClaimsPrinciple(principal, cachedProfile);
                }

                AtProtoHttpResult<ProfileViewDetailed> getProfileResult = await agent.GetProfile(agent.Did).ConfigureAwait(false);

                if (getProfileResult.Succeeded)
                {
                    Logger.TransformerGetProfileSucceeded(agent.Did);
                    cachedProfile = new (getProfileResult.Result, agent.Service.ToString());
                    await Cache.Add(agent.Did, cachedProfile).ConfigureAwait(false);
                    Logger.TransformerCachedClaimsForDid(agent.Did);
                    return SupplementClaimsPrinciple(principal, cachedProfile);
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

    private static ClaimsPrincipal SupplementClaimsPrinciple(ClaimsPrincipal principal, ProfileCacheEntry cachedProfile)
    {
        ClaimsIdentity identity = new(principal.Claims, principal.Identity!.AuthenticationType);

        if (cachedProfile is not null)
        {
            if (cachedProfile.Handle is not null)
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Handle,
                    cachedProfile.Handle!,
                    ClaimValueTypes.String,
                    cachedProfile.Issuer));

                identity.AddClaim(new Claim(
                    System.Security.Claims.ClaimTypes.Name,
                    cachedProfile.Handle!,
                    ClaimValueTypes.String,
                    cachedProfile.Issuer));
            }

            if (!string.IsNullOrEmpty(cachedProfile.DisplayName))
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.DisplayName,
                    cachedProfile.DisplayName,
                    ClaimValueTypes.String,
                    cachedProfile.Issuer));
            }

            if (!string.IsNullOrEmpty(cachedProfile.Description))
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Description,
                    cachedProfile.Description!,
                    ClaimValueTypes.String,
                    cachedProfile.Issuer));
            }

            if (!string.IsNullOrEmpty(cachedProfile.Pronouns))
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Pronouns,
                    cachedProfile.Pronouns!,
                    ClaimValueTypes.String,
                    cachedProfile.Issuer));
            }

            if (cachedProfile.Website is not null)
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Website,
                    cachedProfile.Website.ToString(),
                    ClaimValueTypes.String,
                    cachedProfile.Issuer));
            }

            if (cachedProfile.Avatar is not null)
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Avatar,
                    cachedProfile.Avatar.ToString(),
                    ClaimValueTypes.String,
                    cachedProfile.Issuer));
            }

            if (cachedProfile.Banner is not null)
            {
                identity.AddClaim(new Claim(
                    Bluesky.ClaimTypes.Banner,
                    cachedProfile.Banner.ToString(),
                    ClaimValueTypes.String,
                    cachedProfile.Issuer));
            }
        }

        return new ClaimsPrincipal(identity);
    }
}
