// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Encapsulates the contents of the frame body for a <paramref name="Handle" /> operation.
/// </summary>
/// <param name="Did">The account this event corresponds to.</param>
/// <param name="Seq">The stream sequence number of this message.</param>
/// <param name="Handle">The handle the operation applies to.</param>
/// <param name="Time">The <see cref="DateTimeOffset"/> when this message was originally broadcast.</param>
/// <remarks>
///<para>Creates a new instance of <see cref="HandlePayload"/></para>
/// </remarks>
[SuppressMessage("Code Smell", "S1133: Deprecated code should be removed", Justification = "Could still be present in firehose archives.")]
[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete(
    message: "This message type is no longer issued by the firehose.",
    UrlFormat = "https://github.com/bluesky-social/atproto/blob/8dba37fdea68bad7e5ef1fc6e99eea2957272847/lexicons/com/atproto/sync/subscribeRepos.json#L20")]
public sealed record HandlePayload(Did? Did, long Seq, Handle? Handle, DateTimeOffset Time) : IFramePayload, ICborConvertor<HandlePayload>
{
    internal HandlePayload(CBORObject cborObject)
        : this(
            cborObject["did"] is not null ? new Did(cborObject["did"].AsString()) : null,
            cborObject["seq"].AsInt64Value(),
            cborObject["handle"] is not null ? new Handle(cborObject["handle"].AsString()) : null,
            cborObject["time"].ToDateTimeOffset())
    {
    }

    /// <inheritdoc/>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="cborObject"/> is <see langword="null"/>.</exception>
    public static HandlePayload FromCBORObject(CBORObject cborObject)
    {
        ArgumentNullException.ThrowIfNull(cborObject);

        return new HandlePayload(cborObject);
    }
}
