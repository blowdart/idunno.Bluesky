// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Group;

/// <summary>
/// Join link view to be used within a group view, so the conversation is surrounding, not specified inside this view.
/// </summary>
public sealed record JoinLinkView : View
{
    [JsonConstructor]
    internal JoinLinkView(string code, string enabledStatus, bool requireApproval, string joinRule, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(enabledStatus);
        ArgumentNullException.ThrowIfNull(joinRule);

        Code = code;
        EnabledStatus = enabledStatus;
        RequireApproval = requireApproval;
        JoinRule = joinRule;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Gets the code for the join link.
    /// </summary>
    [JsonRequired]
    public string Code { get; init; }

    /// <summary>
    /// Gets the enabled status of the join link. Known values are held in <see cref="LinkEnabledStatus"/>.
    /// </summary>
    [JsonRequired]
    public string EnabledStatus { get; init; }

    /// <summary>
    /// Gets a flag indicating whether approval is required to join the group.
    /// </summary>
    [JsonRequired]
    public bool RequireApproval { get; init; }

    /// <summary>
    /// Gets the join rule for the group. Known values are held in <see cref="JoinRule"/>.
    /// </summary>
    [JsonRequired]
    public string JoinRule { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the join link was created.
    /// </summary>
    [JsonRequired]
    public DateTimeOffset CreatedAt { get; init; }
}
