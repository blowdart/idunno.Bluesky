// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Feed.Gates;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// User's default post interaction preferences
/// These values should be applied as default values when creating new posts. These refs should mirror the threadgate and postgate records exactly.
/// </summary>
public sealed record PostInteractionSettingsPreferences : Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="PostInteractionSettingsPreferences"/>.
    /// </summary>
    /// <param name="threadGateAllowRules"> List of rules defining who can reply to this users posts.</param>
    /// <param name="postGateEmbeddingRules">List of rules defining who can embed this users posts.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when more than 5 rules are provided for either parameter.</exception>
    [JsonConstructor]
    public PostInteractionSettingsPreferences(ICollection<ThreadGateRule>? threadGateAllowRules, ICollection<PostGateRule>? postGateEmbeddingRules)
    {
        if (threadGateAllowRules is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(threadGateAllowRules.Count, 5);
            ThreadGateAllowRules = threadGateAllowRules;
        }

        if (postGateEmbeddingRules is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(postGateEmbeddingRules.Count, 5);
            PostGateEmbeddingRules = postGateEmbeddingRules;
        }
    }

    /// <summary>
    /// List of rules defining who can reply to this users posts. If value is an empty array, no one can reply. If value is <see langword="null"/>, anyone can reply.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("threadgateAllowRules")]
    public ICollection<ThreadGateRule>? ThreadGateAllowRules { get; init; }

    /// <summary>
    /// List of rules defining who can embed this users posts. If value is an empty array or is <see langword="null"/>, no particular rules apply and anyone can embed.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("postgateEmbeddingRules")]
    public ICollection<PostGateRule>? PostGateEmbeddingRules { get; init; }
}