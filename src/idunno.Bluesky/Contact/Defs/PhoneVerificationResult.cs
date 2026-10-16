// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Contact;
#pragma warning restore IDE0130

/// <summary>
/// Contains the single-use token obtained by verifying a phone number.
/// </summary>
public sealed class PhoneVerificationResult
{
    [JsonConstructor]
    internal PhoneVerificationResult(string token)
    {
        if (token is null)
        {
            throw new JsonException("A phone verification result requires a token.");
        }

        Token = token;
    }

    /// <summary>
    /// Gets the sensitive, single-use token for a contact import.
    /// </summary>
    /// <remarks>
    /// <para>Do not log or persist this token.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public string Token { get; internal init; }
}
