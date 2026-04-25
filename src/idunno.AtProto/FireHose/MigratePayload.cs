// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Encapsulates the information from a migration frame.
/// </summary>
/// <param name="Did">The account this event corresponds to.</param>
/// <param name="Seq">The stream sequence number of this message.</param>
/// <param name="MigrateTo">Where the <paramref name="Did"/> was migrated to.</param>
/// <param name="Time">The <see cref="DateTimeOffset"/> when this message was originally broadcast.</param>
[SuppressMessage("Code Smell", "S1133: Deprecated code should be removed", Justification = "Could still be present in firehose archives.")]
[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete(
    message: "This message type is no longer issued by the firehose.",
    UrlFormat = "https://github.com/bluesky-social/atproto/blob/8dba37fdea68bad7e5ef1fc6e99eea2957272847/lexicons/com/atproto/sync/subscribeRepos.json#L20")]
public record MigratePayload(Did? Did, long Seq, string? MigrateTo, DateTimeOffset Time) : IFramePayload, ICborConvertor<MigratePayload>
{
    internal MigratePayload(CBORObject cborObject) : this(
        cborObject["did"] is not null ? new Did(cborObject["did"].AsString()) : null,
        cborObject["seq"].AsInt64Value(),
        cborObject["migrateTo"]?.AsString(),
        cborObject["time"].ToDateTimeOffset())
    {
    }

    /// <inheritdoc/>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="cborObject"/> is <see langword="null"/>.</exception>
    public static MigratePayload FromCBORObject(CBORObject cborObject)
    {
        ArgumentNullException.ThrowIfNull(cborObject);

        return new MigratePayload(cborObject);
    }
}
