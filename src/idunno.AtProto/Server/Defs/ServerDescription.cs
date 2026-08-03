// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Server;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Information about an AT Proto server
/// </summary>
public sealed record ServerDescription
{
    [JsonConstructor]
    internal ServerDescription(
        Did did,
        bool? inviteCodeRequired,
        bool? phoneVerificationRequired,
        int? blobUploadLimit,
        IReadOnlyList<string> availableUserDomains,
        Links? links,
        Contact? contact)
    {
        ArgumentNullException.ThrowIfNull(did);

        Did = did;

        if (contact is not null && !string.IsNullOrEmpty(contact.Email))
        {
            Contact = contact;
        }

        if (links is not null && (links.PrivacyPolicy is not null || links.TermsOfService is not null))
        {
            Links = links;
        }

        AvailableUserDomains = availableUserDomains;
        InviteCodeRequired = inviteCodeRequired;
        PhoneVerificationRequired = phoneVerificationRequired;
        BlobUploadLimit = blobUploadLimit;
    }

    /// <summary>
    /// Gets the DID of this server.
    /// </summary>
    [JsonRequired]
    public Did Did { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the server needs an invite code.
    /// </summary>
    public bool? InviteCodeRequired { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the server will perform verification on phone numbers.
    /// </summary>
    public bool? PhoneVerificationRequired { get; init; }

    /// <summary>
    /// Gets the maximum size of a blob that can be uploaded via <see cref="AtProtoAgent.UploadBlob(byte[], string, Uri?, string?, CancellationToken)"/>, in bytes.
    /// </summary>
    public int? BlobUploadLimit { get; init; }

    /// <summary>
    /// Gets a list of one or more domains that users can create handles with on this server.
    /// </summary>
    public IReadOnlyList<string> AvailableUserDomains { get; init; } = [];

    /// <summary>
    /// Gets any links the server provides with its description.
    /// </summary>
    public Links? Links { get; init; }

    /// <summary>
    /// Gets any contact information the server provides with its description.
    /// </summary>
    public Contact? Contact { get; init; }
}
