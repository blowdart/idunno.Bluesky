// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Distributed;
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
    private const string CacheKeyPrefix = "bluesky:profileclaims:";

    private const string ErrorIndicator = "**error**";

    /// <summary>
    /// Create a new instance of <see cref="ProfileClaimsTransformer"/>
    /// </summary>
    /// <param name="cache">The <see cref="IDistributedCache"/> to use as a claims source.</param>
    /// <param name="loggerFactory">The <see cref="LoggerFactory"/> to create loggers from.</param>
    /// <param name="options">The <see cref="ProfileClaimsTransformerOptions"/> options to use.</param>
    /// <param name="blueskyAgentOptions">The <see cref="Bluesky.BlueskyAgentOptions"/> to use.</param>
    public ProfileClaimsTransformer(
        IDistributedCache cache,
        ILoggerFactory loggerFactory,
        IOptionsMonitor<ProfileClaimsTransformerOptions> options,
        IOptionsMonitor<BlueskyAgentOptions> blueskyAgentOptions)
    {
        loggerFactory ??= NullLoggerFactory.Instance;

        BlueskyAgentOptions = blueskyAgentOptions;
        Cache = cache;
        Logger = loggerFactory.CreateLogger(GetType().FullName!);
        Options = options;
    }

    IOptionsMonitor<BlueskyAgentOptions> BlueskyAgentOptions { get; }

    private IDistributedCache Cache { get; }

    private ILogger Logger { get; }

    IOptionsMonitor<ProfileClaimsTransformerOptions> Options { get; }

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
                string cacheKey = $"{CacheKeyPrefix}{agent.Did}";
                string? cacheRecord = await Cache.GetStringAsync(cacheKey).ConfigureAwait(false);

                if (cacheRecord is not null && string.Equals(cacheRecord, ErrorIndicator, StringComparison.Ordinal))
                {
                    return principal;
                }
                
                if (cacheRecord is not null)
                {
                    try
                    {
                        HandleDisplayNameCacheEntry? cacheEntry =
                            JsonSerializer.Deserialize(cacheRecord, SourceGenerationContext.Default.HandleDisplayNameCacheEntry);

                        if (cacheEntry is not null)
                        {
                            Logger.TransformerCachedClaimsFound(agent.Did);
                            return SupplementClaimsPrinciple(principal, cacheEntry.Handle, cacheEntry.DisplayName, cacheEntry.Issuer);
                        }
                    }
                    catch (JsonException)
                    {
                        // Swallow
                    }
                }

                AtProtoHttpResult<ProfileViewDetailed> getProfileResult = await agent.GetProfile(agent.Did).ConfigureAwait(false);

                if (getProfileResult.Succeeded)
                {
                    Logger.TransformerGetProfileSucceeded(agent.Did);
                    HandleDisplayNameCacheEntry cacheEntry = new (getProfileResult.Result.Handle, getProfileResult.Result.DisplayName, agent.Service.ToString());

                    try
                    {
                        string? cacheEntryJson = JsonSerializer.Serialize(cacheEntry, SourceGenerationContext.Default.HandleDisplayNameCacheEntry);

                        if (cacheEntryJson is not null)
                        {
                            await Cache.SetStringAsync(cacheKey, 
                                cacheEntryJson, new DistributedCacheEntryOptions
                                {
                                    AbsoluteExpirationRelativeToNow = Options.CurrentValue.CacheTimeout
                                }).ConfigureAwait(false);
                            Logger.TransformerCachedClaimsForDid(agent.Did);
                        }
                    }
                    catch (JsonException)
                    {
                        // Swallow
                    }

                    return SupplementClaimsPrinciple(principal, getProfileResult.Result.Handle, getProfileResult.Result.DisplayName, agent.Service.ToString());
                }
                else
                {
                    Logger.TransformerGetProfileFailed(agent.Did, getProfileResult.StatusCode, getProfileResult.AtErrorDetail?.Error, getProfileResult.AtErrorDetail?.Message);
                    await Cache.SetStringAsync(cacheKey, ErrorIndicator).ConfigureAwait(false);
                }
            }
        }

        return principal;
    }

    private static ClaimsPrincipal SupplementClaimsPrinciple(ClaimsPrincipal principal, Handle? handle, string? displayName, string issuer)
    {
        ClaimsIdentity identity = new(principal.Claims, principal.Identity!.AuthenticationType);

        if (handle is not null)
        {
            identity.AddClaim(new Claim(
                Bluesky.ClaimTypes.Handle,
                handle!,
                ClaimValueTypes.String,
                issuer));

            identity.AddClaim(new Claim(
                System.Security.Claims.ClaimTypes.Name,
                handle!.Value,
                ClaimValueTypes.String,
                issuer));
        }

        if (!string.IsNullOrEmpty(displayName))
        {
            identity.AddClaim(new Claim(
                Bluesky.ClaimTypes.DisplayName,
                displayName,
                ClaimValueTypes.String,
                issuer));
        }

        return new ClaimsPrincipal(identity);
    }
}

/// <summary>
/// Defines an entry in the display name cache.
/// </summary>
/// <param name="Handle">The <see cref="AtProto.Handle"/> to cache.</param>
/// <param name="DisplayName">The display name to cache.</param>
/// <param name="Issuer">The issuer of the display name.</param>
public sealed record HandleDisplayNameCacheEntry(Handle Handle, string? DisplayName, string Issuer);
