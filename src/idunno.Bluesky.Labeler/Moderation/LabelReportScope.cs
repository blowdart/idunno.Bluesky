// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Captures immutable declaration restrictions for report intake.</summary>
/// <remarks>
/// <para>Subject types are explicit and limited to accounts and records. Missing reason or collection
/// restrictions permit all values, including future reasons; empty restrictions permit none.
/// Report scope is independent of permission to issue labels.</para>
/// </remarks>
public sealed class LabelReportScope
{
    /// <summary>Initializes a new instance of the <see cref="LabelReportScope"/> class.</summary>
    /// <param name="subjectTypes">The explicitly supported account and record subject types.</param>
    /// <param name="reasonTypes">The optional allowed reason identifiers.</param>
    /// <param name="subjectCollections">The optional allowed record collections.</param>
    /// <exception cref="ArgumentNullException"><paramref name="subjectTypes"/> or a collection element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A collection contains duplicate, blank, invalid or unsupported values.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A collection or text value exceeds its supported bound.</exception>
    public LabelReportScope(
        IReadOnlyCollection<string> subjectTypes,
        IReadOnlyCollection<string>? reasonTypes = null,
        IReadOnlyCollection<Nsid>? subjectCollections = null)
    {
        ArgumentNullException.ThrowIfNull(subjectTypes);
        SubjectTypes = Copy(subjectTypes, nameof(subjectTypes));
        if (SubjectTypes.Any(subject => subject is not ("account" or "record")))
        {
            throw new ArgumentException("Only account and record reports are supported.", nameof(subjectTypes));
        }

        ReasonTypes = reasonTypes is null ? null : Copy(reasonTypes, nameof(reasonTypes));
        if (subjectCollections is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(subjectCollections.Count, 1000);
            Nsid[] collections = [.. subjectCollections];
            HashSet<string> unique = new(StringComparer.Ordinal);
            foreach (Nsid? collection in collections)
            {
                ArgumentNullException.ThrowIfNull(collection);
                if (!unique.Add(collection.ToString()))
                {
                    throw new ArgumentException("Collections must be unique.", nameof(subjectCollections));
                }
            }

            SubjectCollections = new ReadOnlyCollection<Nsid>(collections);
        }
    }

    /// <summary>Gets the explicitly accepted subject types.</summary>
    public IReadOnlyList<string> SubjectTypes { get; }

    /// <summary>Gets allowed reason identifiers, or <see langword="null"/> to permit all reasons.</summary>
    public IReadOnlyList<string>? ReasonTypes { get; }

    /// <summary>Gets allowed record collections, or <see langword="null"/> to permit all collections.</summary>
    public IReadOnlyList<Nsid>? SubjectCollections { get; }

    /// <summary>Determines whether a subject and reason are within the declared report scope.</summary>
    /// <param name="subject">The submitted account or record version.</param>
    /// <param name="reasonType">The nonblank report reason identifier.</param>
    /// <returns><see langword="true"/> if both are within scope; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reasonType"/> is invalid text.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reasonType"/> exceeds 1,024 UTF-8 bytes.</exception>
    public bool Accepts(LabelReportSubject subject, string reasonType)
    {
        ArgumentNullException.ThrowIfNull(subject);
        LabelInput.ValidateText(reasonType, nameof(reasonType), 1024);
        return SubjectTypes.Contains(subject.SubjectType, StringComparer.Ordinal) &&
            (ReasonTypes is null || ReasonTypes.Contains(reasonType, StringComparer.Ordinal)) &&
            (subject.RecordUri is null || SubjectCollections is null || SubjectCollections.Contains(subject.RecordUri.Collection!));
    }

    /// <summary>Copies bounded ordinal sets without retaining caller-owned collections.</summary>
    private static ReadOnlyCollection<string> Copy(IReadOnlyCollection<string> source, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(source.Count, 1000);
        string[] values = new string[source.Count];
        HashSet<string> unique = new(StringComparer.Ordinal);
        int index = 0;
        foreach (string value in source)
        {
            string validated = LabelInput.ValidateText(value, parameterName, 1024);
            if (!unique.Add(validated))
            {
                throw new ArgumentException("Values must be unique using ordinal comparison.", parameterName);
            }

            values[index++] = validated;
        }

        return new(values);
    }
}
