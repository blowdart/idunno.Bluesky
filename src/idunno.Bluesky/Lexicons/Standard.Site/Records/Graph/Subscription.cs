// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Standard.Site.Graph;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Record declaring a standard.site subscription to a publication.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(Subscription), "site.standard.graph.subscription")]
public record Subscription : AtProtoRecord
{
    /// <summary>
    /// Creates a new instance of the <see cref="Subscription"/> class.
    /// </summary>
    /// <param name="publication">An <see cref="AtUri"/> reference to the publication record being subscribed to (ex: at://did:plc:abc123/site.standard.publication/xyz789)</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="publication"/> is <see langword="null"/>.</exception>
    public Subscription(AtUri publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        Publication = publication;
    }

    /// <summary>
    /// Gets the reference to the publication being subscribed to.
    /// </summary>
    [JsonRequired]
    public AtUri Publication { get; init; }
}
