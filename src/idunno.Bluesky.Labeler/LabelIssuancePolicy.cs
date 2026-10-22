// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using idunno.AtProto;

namespace idunno.Bluesky.Labeler;

/// <summary>Describes a versioned immutable policy snapshot for new label issuance.</summary>
/// <remarks>
/// <para>Declared values and allowed collections are defensively copied. A <see langword="null"/> collection restriction permits any record collection, while an empty collection permits none.</para>
/// <para>Collection restrictions affect new record issuance only; they do not constrain withdrawals of prior decisions.</para>
/// </remarks>
public sealed class LabelIssuancePolicy
{
    /// <summary>Initializes a new instance of the <see cref="LabelIssuancePolicy"/> class.</summary>
    /// <param name="version">The nonblank version of this policy snapshot.</param>
    /// <param name="declaredValues">The nonblank label values declared by the active policy.</param>
    /// <param name="allowAccounts"><see langword="true"/> to allow new account issuance; otherwise, <see langword="false"/>.</param>
    /// <param name="allowRecords"><see langword="true"/> to allow new record issuance; otherwise, <see langword="false"/>.</param>
    /// <param name="allowedRecordCollections">The optional set of record collections permitted for issuance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="declaredValues"/> or an element in either supplied collection is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The version or a declared value is blank, contains U+0000, is invalid UTF-8, or a collection contains duplicate entries.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The version exceeds 256 UTF-8 bytes or a declared value exceeds 128 UTF-8 bytes.</exception>
    public LabelIssuancePolicy(
        string version,
        IReadOnlyCollection<string> declaredValues,
        bool allowAccounts,
        bool allowRecords,
        IReadOnlyCollection<Nsid>? allowedRecordCollections = null)
    {
        Version = LabelInput.ValidateText(version, nameof(version), 256);
        ArgumentNullException.ThrowIfNull(declaredValues);

        string[] values = new string[declaredValues.Count];
        HashSet<string> uniqueValues = new(StringComparer.Ordinal);
        int index = 0;
        foreach (string? value in declaredValues)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(declaredValues), "A declared value cannot be null.");
            }

            string validatedValue = LabelInput.ValidateText(value, nameof(declaredValues), 128);
            if (!uniqueValues.Add(validatedValue))
            {
                throw new ArgumentException("Declared values must be unique using ordinal comparison.", nameof(declaredValues));
            }

            values[index++] = validatedValue;
        }

        DeclaredValues = new ReadOnlyCollection<string>(values);
        AllowAccounts = allowAccounts;
        AllowRecords = allowRecords;

        if (allowedRecordCollections is not null)
        {
            Nsid[] collections = new Nsid[allowedRecordCollections.Count];
            HashSet<string> uniqueCollections = new(StringComparer.Ordinal);
            index = 0;
            foreach (Nsid? collection in allowedRecordCollections)
            {
                if (collection is null)
                {
                    throw new ArgumentNullException(nameof(allowedRecordCollections), "A record collection cannot be null.");
                }

                if (!uniqueCollections.Add(collection.ToString()))
                {
                    throw new ArgumentException("Allowed record collections must be unique using ordinal comparison.", nameof(allowedRecordCollections));
                }

                collections[index++] = collection;
            }

            AllowedRecordCollections = new ReadOnlyCollection<Nsid>(collections);
        }
    }

    /// <summary>Gets the version of this policy snapshot.</summary>
    /// <value>The nonblank policy version.</value>
    public string Version { get; }

    /// <summary>Gets the immutable collection of declared label values.</summary>
    /// <value>A defensive read-only snapshot of the declared values.</value>
    public IReadOnlyCollection<string> DeclaredValues { get; }

    /// <summary>Gets a value that indicates whether account issuance is permitted.</summary>
    /// <value><see langword="true"/> if account issuance is permitted; otherwise, <see langword="false"/>.</value>
    public bool AllowAccounts { get; }

    /// <summary>Gets a value that indicates whether record issuance is permitted.</summary>
    /// <value><see langword="true"/> if record issuance is permitted; otherwise, <see langword="false"/>.</value>
    public bool AllowRecords { get; }

    /// <summary>Gets the optional record-collection restriction.</summary>
    /// <value>A read-only collection, <see langword="null"/> to permit any collection, or an empty collection to permit none.</value>
    public IReadOnlyCollection<Nsid>? AllowedRecordCollections { get; }
}
