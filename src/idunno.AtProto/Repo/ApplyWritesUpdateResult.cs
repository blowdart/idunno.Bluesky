// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Repo;

/// <summary>
/// Encapsulates the result from a update operation to the applyWrites API.
/// </summary>
public sealed record ApplyWritesUpdateResult : IApplyWritesResult
{
    internal ApplyWritesUpdateResult(ApplyWritesUpdateResponse applyWritesUpdateResponse)
    {
        ArgumentNullException.ThrowIfNull(applyWritesUpdateResponse);

        Uri = applyWritesUpdateResponse.Uri;
        Cid = applyWritesUpdateResponse.Cid;
    }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the record the write operation updated.
    /// </summary>
    public AtUri Uri { get; init; }

    /// <summary>
    /// Gets the <see cref="Cid"/> of the record the write operation updated.
    /// </summary>
    public Cid Cid { get; init; }

    /// <summary>
    /// Gets the <see cref="StrongReference"/> of the record the write operation updated.
    /// </summary>
    /// <remarks>
    /// <para>Calculated from the current <see cref="Uri"/> and <see cref="Cid"/>, so it stays correct when either is
    /// changed by a <see langword="with"/> expression.</para>
    /// </remarks>
    public StrongReference StrongReference => new(Uri, Cid);
}