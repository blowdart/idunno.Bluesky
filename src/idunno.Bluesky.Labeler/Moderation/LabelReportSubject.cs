// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Identifies an account or an exact record version submitted for review.</summary>
/// <remarks>
/// <para>A report is evidence about the submitted identity, not proof that the account or record currently
/// exists. Record subjects require a CID and a DID repository; intake does not resolve mutable handles.</para>
/// <para>Case grouping includes the record CID. This differs from label reconciliation, whose identity
/// remains issuer, subject URI and label value.</para>
/// </remarks>
public sealed class LabelReportSubject
{
    /// <summary>Retains a subject validated by an account or record factory.</summary>
    private LabelReportSubject(Did repository, AtUri? recordUri, Cid? cid)
    {
        Repository = repository;
        RecordUri = recordUri;
        Cid = cid;
    }

    /// <summary>Creates an account report subject.</summary>
    /// <param name="account">The account DID.</param>
    /// <returns>The account subject.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="account"/> is <see langword="null"/>.</exception>
    public static LabelReportSubject Account(Did account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new(account, null, null);
    }

    /// <summary>Creates an exact record-version report subject.</summary>
    /// <param name="uri">The complete record AT URI with a DID repository.</param>
    /// <param name="cid">The submitted record CID.</param>
    /// <returns>The record subject.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="uri"/> is incomplete or uses a handle repository.</exception>
    public static LabelReportSubject Record(AtUri uri, Cid cid)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(cid);
        if (uri.Repo is not Did repository || uri.Collection is null || uri.RecordKey is null)
        {
            throw new ArgumentException("A report requires a complete record URI with a DID repository.", nameof(uri));
        }

        return new(repository, uri, cid);
    }

    /// <summary>Gets the account or record repository DID.</summary>
    public Did Repository { get; }

    /// <summary>Gets the record URI, or <see langword="null"/> for an account.</summary>
    public AtUri? RecordUri { get; }

    /// <summary>Gets the record CID, or <see langword="null"/> for an account.</summary>
    public Cid? Cid { get; }

    /// <summary>Gets the account DID or record AT URI.</summary>
    public string Subject => RecordUri?.ToString() ?? Repository.Value;

    /// <summary>Gets the declaration's subject-type identifier.</summary>
    public string SubjectType => RecordUri is null ? "account" : "record";
}
