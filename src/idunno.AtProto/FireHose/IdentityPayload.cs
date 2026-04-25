// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Encapsulates the information from an identity frame.
/// </summary>
/// <param name="Seq">The stream sequence number of this message.</param>
/// <param name="Did">The account this event corresponds to.</param>
/// <param name="Time">The <see cref="DateTimeOffset"/> when this message was originally broadcast.</param>
/// <param name="Handle">An optional <see cref="AtProto.Handle"/> to which the operation applied.</param>
public record IdentityPayload(long Seq, Did Did, DateTimeOffset Time, Handle? Handle) : IFramePayload, ICborConvertor<IdentityPayload>
{
    internal IdentityPayload(CBORObject cborObject)
        : this(
            cborObject["seq"].AsInt64Value(),
            new Did(cborObject["did"].AsString()),
            cborObject["time"].ToDateTimeOffset(),
            cborObject.ContainsKey("handle") ? new Handle(cborObject["handle"].AsString()) : null)
    {
    }

    /// <inheritdoc/>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="cborObject"/> is <see langword="null"/>.</exception>
    public static IdentityPayload FromCBORObject(CBORObject cborObject)
    {
        ArgumentNullException.ThrowIfNull(cborObject);

        return new IdentityPayload(cborObject);
    }
}
