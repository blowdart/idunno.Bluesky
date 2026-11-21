// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo;

/// <summary>
/// An abstract class for an <see cref="AtProtoObject"/> that has an <see cref="AtUri"/> and <see cref="AtProto.Cid"/>.
/// </summary>
public abstract record AtProtoRepositoryObject : AtProtoObject
{
    /// <summary>
    /// Sets the strong reference properties for the <see cref="AtProtoRepositoryObject"/>
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the record.</param>
    /// <param name="cid">The <see cref="AtProto.Cid"/> of the record.</param>
    /// <exception cref="ArgumentNullException">Throws if <paramref name="uri"/> or <paramref name="cid"/> is <see langword="null"/>.</exception>
    protected AtProtoRepositoryObject(AtUri uri, Cid cid)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(cid);

        Uri = uri;
        CidOrDefault = cid;
    }

    /// <summary>
    /// Sets the <see cref="Uri"/> for an <see cref="AtProtoRepositoryObject"/> whose <see cref="AtProto.Cid"/> may be absent.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the record.</param>
    /// <param name="cid">The <see cref="AtProto.Cid"/> of the record, or <see langword="null"/> if the server did not supply one.</param>
    /// <param name="cidIsOptional"><see langword="true"/> if <paramref name="cid"/> may be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Throws if <paramref name="uri"/> is <see langword="null"/>, or if <paramref name="cid"/> is <see langword="null"/> and <paramref name="cidIsOptional"/> is <see langword="false"/>.</exception>
    /// <remarks>
    /// <para>Repository reads such as <c>com.atproto.repo.getRecord</c> make <paramref name="cid"/> optional. Only repository record
    /// types use this constructor; other repository objects must always have a <see cref="AtProto.Cid"/>.</para>
    /// </remarks>
    private protected AtProtoRepositoryObject(AtUri uri, Cid? cid, bool cidIsOptional)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!cidIsOptional)
        {
            ArgumentNullException.ThrowIfNull(cid);
        }

        Uri = uri;
        CidOrDefault = cid;
    }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the record.
    /// </summary>
    [JsonRequired]
    public AtUri Uri { get; init; }

    /// <summary>
    /// Gets the <see cref="AtProto.Cid">Content Identifier</see> of the record.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when getting the value and the record has no <see cref="AtProto.Cid"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when setting the value to <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>A server may omit the CID of a record it returns from <c>com.atproto.repo.getRecord</c>. Check <see cref="HasCid"/>
    /// before reading this property when a record came from such a read.</para>
    /// </remarks>
    [JsonRequired]
    public Cid Cid
    {
        get => CidOrDefault ?? throw new InvalidOperationException($"The record '{Uri}' has no CID. Check {nameof(HasCid)} before reading {nameof(Cid)}.");
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            CidOrDefault = value;
        }
    }

    /// <summary>
    /// Gets a flag indicating whether the record has a <see cref="AtProto.Cid"/>.
    /// </summary>
    [JsonIgnore]
    public bool HasCid => CidOrDefault is not null;

    /// <summary>
    /// Gets a <see cref="StrongReference"/> for the record.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the record has no <see cref="AtProto.Cid"/>.</exception>
    /// <remarks>
    /// <para>Calculated from the current <see cref="Uri"/> and <see cref="Cid"/>, so it stays correct when either is
    /// changed by a <see langword="with"/> expression.</para>
    /// <para>A strong reference requires a CID. Use <see cref="TryGetStrongReference(out StrongReference?)"/> when the record
    /// may not have one.</para>
    /// </remarks>
    public StrongReference StrongReference => new(Uri, Cid);

    /// <summary>
    /// Gets the <see cref="AtProto.Cid"/> of the record, or <see langword="null"/> when it is absent.
    /// </summary>
    internal Cid? CidOrDefault { get; private init; }

    /// <summary>
    /// Gets a <see cref="StrongReference"/> for the record, if the record has a <see cref="AtProto.Cid"/>.
    /// </summary>
    /// <param name="strongReference">When this method returns <see langword="true"/>, the <see cref="StrongReference"/> for the record; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the record has a <see cref="AtProto.Cid"/>; otherwise <see langword="false"/>.</returns>
    public bool TryGetStrongReference([NotNullWhen(true)] out StrongReference? strongReference)
    {
        strongReference = CidOrDefault is null ? null : new StrongReference(Uri, CidOrDefault);
        return strongReference is not null;
    }

    /// <summary>
    /// Appends the record's members to the specified <paramref name="builder"/> for <see cref="object.ToString"/>.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append members to.</param>
    /// <returns><see langword="true"/> if members were appended.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Written explicitly so formatting a record without a <see cref="AtProto.Cid"/> does not throw.</para>
    /// </remarks>
    protected override bool PrintMembers(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Append("Uri = ").Append(Uri);
        builder.Append(", Cid = ").Append(CidOrDefault);
        builder.Append(", HasCid = ").Append(HasCid);
        return true;
    }
}
