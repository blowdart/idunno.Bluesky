// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Metadata associated with an actor's activity subscription preferences.
/// </summary>
/// <param name="AllowSubscriptions">Value indicating the actor's subscription preferences. Known values are defined in <see cref="AllowSubscriptionsKnownValues"/>.</param>
public sealed record ProfileAssociatedActivitySubscription(string AllowSubscriptions)
{
}

/// <summary>
/// Known values for the <see cref="ProfileAssociatedActivitySubscription.AllowSubscriptions"/> property.
/// </summary>
public static class AllowSubscriptionsKnownValues
{
    /// <summary>
    /// Gets a value indicating that the actor allows subscriptions from followers.
    /// </summary>
    public const string Followers = "followers";

    /// <summary>
    /// Gets a value indicating that the actor allows subscriptions from mutuals.
    /// </summary>
    public const string Mutuals = "mutuals";

    /// <summary>
    /// Gets a value indicating that the actor does not allow subscriptions.
    /// </summary>
    public const string None = "none";
}
