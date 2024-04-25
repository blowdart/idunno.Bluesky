// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Bluesky
{
    // https://atproto.blue/en/latest/atproto/atproto_client.models.com.atproto.label.defs.html#atproto_client.models.com.atproto.label.defs.Label

    /// <summary>
    /// Represents the parts of a Label
    /// </summary>
    /// <param name="Ver">The version of the label.</param>
    /// <param name="Src">DID of the actor who created this label.</param>
    /// <param name="Uri">AT URI of the record, repository (account), or other resource that this label applies to.</param>
    /// <param name="Cid">An optional  Content Identifier specifying the specific version of the resource this label applies to.</param>
    /// <param name="Val">The short string name of the value or type of this label.</param>
    /// <param name="Neg">If true, this is a negation label, overwriting a previous label.</param>
    /// <param name="Cts">Timestamp when this label was created.</param>
    /// <param name="Sig">Signature of dag-cbor encoded label.</param>
    public record Label(int? Ver, Did Src, AtUri Uri, string? Cid, string Val, bool? Neg, DateTime Cts, byte[]? Sig);
}
