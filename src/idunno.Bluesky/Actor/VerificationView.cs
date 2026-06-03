// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Actor;

/// <summary>
/// An individual verification for an associated subject.
/// </summary>
public sealed record VerificationView
{
    [JsonConstructor]
    internal VerificationView(
        Did issuer,
        AtUri uri,
        bool isValid,
        DateTimeOffset createdAt,
        string? issuerDisplayName,
        Handle? issuerHandle)
    {
        Issuer = issuer;
        Uri = uri;
        IsValid = isValid;
        CreatedAt = createdAt;
        IssuerDisplayName = issuerDisplayName;
        IssuerHandle = issuerHandle;
    }

    /// <summary>
    /// Gets the actor who issued the verification.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public Did Issuer { get; init; }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the verification record.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public AtUri Uri { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the verification passes validation.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the verification was created.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset CreatedAt { get; init; }
    
    /// <summary>
    /// Gets the display name of the actor who issued the verification.
    /// </summary>
    [JsonInclude]
    public string? IssuerDisplayName { get; init; }

    /// <summary>
    /// Gets the <see cref="Handle"/> of the actor who issued the verification.
    /// </summary>
    public Handle? IssuerHandle { get; init; }
}
