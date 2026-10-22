// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler;

/// <summary>Identifies an account or a complete record URI for a label operation.</summary>
/// <remarks>
/// <para>Record targets accept any valid AT Protocol collection and record key. This type does not impose an application-specific collection allowlist.</para>
/// <para>Account identifiers may be handles before resolution. The internal repository replacement helper produces the DID-based target used for signed decisions.</para>
/// </remarks>
public sealed class LabelTarget
{
    /// <summary>Initializes a new instance of the <see cref="LabelTarget"/> class using a shape validated by its factory.</summary>
    /// <remarks><para>Private construction prevents account targets from carrying record-only fields
    /// and keeps the repository consistent with a record target's URI.</para></remarks>
    private LabelTarget(
        LabelTargetKind kind,
        AtIdentifier repository,
        AtUri? recordUri,
        Cid? cid)
    {
        Kind = kind;
        Repository = repository;
        RecordUri = recordUri;
        Cid = cid;
    }

    /// <summary>Creates an account target.</summary>
    /// <param name="account">The account DID or handle.</param>
    /// <returns>An account target.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="account"/> is <see langword="null"/>.</exception>
    public static LabelTarget Account(AtIdentifier account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new LabelTarget(
            kind: LabelTargetKind.Account,
            repository: account,
            recordUri: null,
            cid: null);
    }

    /// <summary>Creates a record target from a complete AT Protocol record locator.</summary>
    /// <param name="uri">The locator containing a repository, collection and record key.</param>
    /// <param name="cid">An optional expected record version.</param>
    /// <returns>A record target.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="uri"/> does not identify a complete record.</exception>
    public static LabelTarget Record(AtUri uri, Cid? cid = null)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.Collection is null || uri.RecordKey is null)
        {
            throw new ArgumentException("A record locator must contain a collection and record key.", nameof(uri));
        }

        return new LabelTarget(
            kind: LabelTargetKind.Record,
            repository: uri.Repo,
            recordUri: uri,
            cid: cid);
    }

    /// <summary>Gets the kind of subject targeted by this instance.</summary>
    /// <value>The target kind.</value>
    public LabelTargetKind Kind { get; }

    /// <summary>Gets the repository or account identifier supplied for this target.</summary>
    /// <value>The repository identifier.</value>
    public AtIdentifier Repository { get; }

    /// <summary>Gets the complete record URI for a record target.</summary>
    /// <value>The record URI, or <see langword="null"/> for an account target.</value>
    public AtUri? RecordUri { get; }

    /// <summary>Gets the optional expected version of the target record.</summary>
    /// <value>The expected record CID, or <see langword="null"/> when no version was specified.</value>
    public Cid? Cid { get; }

    /// <summary>Gets the signed subject identifier for the target.</summary>
    /// <value>The account DID or record URI as a string.</value>
    /// <remarks>
    /// <para>The value reflects the target's current repository identifier; callers resolve handle-based repositories before using it as a signed subject.</para>
    /// </remarks>
    internal string Subject => Kind switch
    {
        LabelTargetKind.Account => Repository.Value,
        LabelTargetKind.Record => RecordUri!.ToString(),
        _ => throw new InvalidOperationException("The target kind is not supported.")
    };

    /// <summary>Returns this target with its repository replaced by a resolved DID.</summary>
    /// <param name="repository">The resolved repository DID.</param>
    /// <returns>A new target retaining the record path and CID, if any.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="repository"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This helper creates a DID-based identity without changing collection, record key or requested CID.</para>
    /// </remarks>
    internal LabelTarget WithRepository(Did repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        return Kind switch
        {
            LabelTargetKind.Account => Account(repository),
            LabelTargetKind.Record => Record(new AtUri($"at://{repository}/{RecordUri!.Collection}/{RecordUri.RecordKey}"), Cid),
            _ => throw new InvalidOperationException("The target kind is not supported.")
        };
    }
}
