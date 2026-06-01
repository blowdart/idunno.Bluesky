// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
using System.Text.Json.Serialization;

namespace GermNetwork.Com;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A declaration of who can message this account
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(MessageMe), "com.germnetwork.declaration#messageMe")]
public record MessageMe
{
    /// <summary>
    /// Creates a new instance of <see cref="MessageMe"/>.
    /// </summary>
    /// <param name="messageMeUrl">The URI to present to an account that does not have its own com.germnetwork.declaration record.</param>
    /// <param name="showButtonTo">The policy of who can message the account.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messageMeUrl"/> or <paramref name="showButtonTo"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="messageMeUrl"/> is less than 1 character or greater than 2047 characters,
    /// or when <paramref name="showButtonTo"/> is less than 1 character or greater than 100 characters.</exception>
    [JsonConstructor]
    public MessageMe(Uri messageMeUrl, string showButtonTo)
    {
        ArgumentNullException.ThrowIfNull(messageMeUrl);
        ArgumentOutOfRangeException.ThrowIfLessThan(messageMeUrl.ToString().Length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan( messageMeUrl.ToString().Length, 2047);

        ArgumentNullException.ThrowIfNull(showButtonTo);
        ArgumentOutOfRangeException.ThrowIfLessThan(showButtonTo.Length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(showButtonTo.Length, 100);


        MessageMeUrl = messageMeUrl;
        ShowButtonTo = showButtonTo;
    }

    /// <summary>
    /// Gets or sets a URI to present to an account that does not have its own com.germnetwork.declaration record, must have an empty fragment component,
    /// where the app should fill in the fragment component with the DIDs of the two accounts who wish to message each other.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1 character or greater than 2047 characters.</exception>
    public Uri MessageMeUrl
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            ArgumentOutOfRangeException.ThrowIfLessThan(value.ToString().Length, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.ToString().Length, 2047);

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the policy of who can message the account, this value is included in the keyPackage, but is duplicated here to allow applications to decide if they should
    /// show a 'Message on Germ' button to the viewer.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1 character or greater than 100 characters.</exception>
    /// <remarks>
    /// <para>Known values are contained in <see cref="ShowButtonToKnownValues"/>.</para>
    /// </remarks>
    public string ShowButtonTo
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentOutOfRangeException.ThrowIfLessThan(value.Length, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, 100);
            field = value;
        }
    }
}

/// <summary>
/// Known values for <see cref="MessageMe.ShowButtonTo"/>
/// </summary>
public static class ShowButtonToKnownValues
{
    /// <summary>
    /// Show the message button to no-one.
    /// </summary>
    public const string None = "none";

    /// <summary>
    /// Show the message button to actors followed by the user.
    /// </summary>
    public const string UsersIFollow = "usersIFollow";

    /// <summary>
    /// Show the message button to everyone.
    /// </summary>
    public const string Everyone = "everyone";

}
