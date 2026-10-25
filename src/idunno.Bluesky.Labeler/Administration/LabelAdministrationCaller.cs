// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Issuance;

namespace idunno.Bluesky.Labeler.Administration;

/// <summary>Represents provider-issued authentication evidence for one administrative invocation.</summary>
/// <remarks><para>Only an authentication adapter may establish this evidence. Actor fields in requests
/// and <see cref="LabelOperationContext"/> do not establish authentication. Membership and credentials
/// are checked again for every operation and at mutation commit.</para></remarks>
public sealed class LabelAdministrationCaller
{
    /// <summary>Initializes immutable evidence after provider authentication.</summary>
    internal LabelAdministrationCaller(
        Did issuer,
        Did actor,
        long generation,
        string credential,
        string? localIdentity,
        DateTimeOffset expiresAt)
    {
        Issuer = issuer;
        Actor = actor;
        Generation = generation;
        Credential = credential;
        LocalIdentity = localIdentity;
        ExpiresAt = expiresAt;
    }

    /// <summary>Gets the labeler issuer.</summary>
    public Did Issuer { get; }
    /// <summary>Gets the authenticated or explicitly delegated team DID.</summary>
    public Did Actor { get; }
    /// <summary>Gets the authenticated OS identity, or <see langword="null"/> for service authentication.</summary>
    public string? LocalIdentity { get; }
    internal long Generation { get; }
    internal string Credential { get; }
    internal DateTimeOffset ExpiresAt { get; }
}
