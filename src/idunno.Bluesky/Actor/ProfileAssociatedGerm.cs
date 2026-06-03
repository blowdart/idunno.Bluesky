// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor;

/// <summary>
/// Germ messaging details associated with a profile
/// </summary>
public record ProfileAssociatedGerm
{
    /// <summary>
    /// Creates a new instance of <see cref="ProfileAssociatedGerm"/>.
    /// </summary>
    /// <param name="messageMeUrl">The URI to message the user.</param>
    /// <param name="showButtonTo">Who the message button should be shown to. Current known values contained in <see cref="ShowButtonToKnownValues"/></param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="messageMeUrl"/> or <paramref name="showButtonTo"/> is <see langword="null" />.</exception>
    public ProfileAssociatedGerm(
        Uri messageMeUrl,
        string showButtonTo)
    {
        ArgumentNullException.ThrowIfNull(messageMeUrl);
        ArgumentNullException.ThrowIfNull(showButtonTo);

        MessageMeUrl = messageMeUrl;
        ShowButtonTo = showButtonTo;
    }

    /// <summary>
    /// The URI to message the user.
    /// </summary>
    [JsonRequired]
    public Uri MessageMeUrl { get; set; }

    /// <summary>
    /// Who the message button should be shown to. Current known values contained in <see cref="ShowButtonToKnownValues"/>
    /// </summary>
    [JsonRequired]
    public string ShowButtonTo { get; set; }
}

/// <summary>
/// Known values for the <see cref="ProfileAssociatedGerm.ShowButtonTo"/> property.
/// </summary>
public static class ShowButtonToKnownValues
{
    /// <summary>
    /// Show the message me button to everyone.
    /// </summary>
    public const string Everyone = "everyone";

    /// <summary>
    /// Show the message me button to users I follow.
    /// </summary>
    public const string UsersIFollow = "usersIFollow";
}
