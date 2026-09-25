// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Server;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Contact information a server may provide as part of its description.
/// </summary>
public sealed record Contact
{
    /// <summary>
    /// Creates a new instance of <see cref="Contact"/>
    /// </summary>
    /// <param name="email">The email address for the contact.</param>
    [JsonConstructor]
    public Contact(string email)
    {
        Email = email;
    }

    /// <summary>
    /// Gets the email address associated with the server.
    /// </summary>
    [JsonInclude]
    public string Email { get; init; }

    /// <summary>
    /// Provides a string representation of this Contact.
    /// </summary>
    /// <returns>The string representation of this contact.</returns>
    public override string ToString() => Email;
}