// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Encapsulates a repo operation.
/// </summary>
public sealed record RepoOp : IFramePayload, ICborConvertor<RepoOp>
{
    /// <summary>
    /// Creates a new instance of <see cref="RepoOp"/> from the specified parameters.
    /// </summary>
    /// <param name="cid">For create and update actions the new record <see cref="AtProto.Cid">CID</see>. For delete actions <see langword="null"/>.</param>
    /// <param name="path">The path the repo action refers to, if any.</param>
    /// <param name="action">The action taken, if any.</param>
    /// <param name="prev">For update and delete action the previous record <see cref="AtProto.Cid">CID</see>. For create actions <see langword="null"/>.</param>
    public RepoOp(Cid? cid, string path, string action, Cid? prev)
    {
        Cid = cid;
        Path = path;
        Action = action;
        Prev = prev;
    }

    internal RepoOp(CBORObject cborObject)
    {
        Cid = cborObject["cid"].ToCid();
        Path = cborObject["path"].AsString();
        Action = cborObject["action"].AsString();
        Prev = cborObject["prev"].ToCid();
    }

    /// <inheritdoc/>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="cborObject"/> is <see langword="null"/>.</exception>
    public static RepoOp FromCBORObject(CBORObject cborObject)
    {
        ArgumentNullException.ThrowIfNull(cborObject);

        return new RepoOp(cborObject);
    }

    /// <summary>
    /// For create and update <see cref="Action"/>s gets the new record <see cref="AtProto.Cid">CID</see>.
    /// For delete actions <see langword="null"/>.
    /// </summary>
    public Cid? Cid { get; }

    /// <summary>
    /// Gets the path the action applies to, if any.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Gets the action performed in the operation, if any.
    /// </summary>
    public string? Action { get; }

    /// <summary>
    /// For create and update <see cref="Action"/>s the new record <see cref="AtProto.Cid">CID</see>.
    /// For delete actions <see langword="null"/>.
    /// </summary>
    public Cid? Prev { get; }
}
