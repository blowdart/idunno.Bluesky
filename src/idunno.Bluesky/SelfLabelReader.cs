// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;

namespace idunno.Bluesky;

/// <summary>
/// Extracts the values of labels that an actor appears to have applied to their own records.
/// </summary>
internal static class SelfLabelReader
{
    private const int HashSetThreshold = 8;

    /// <summary>
    /// Gets the distinct values, in their original order, of the <paramref name="labels"/> applied by <paramref name="source"/> to <paramref name="uri"/>.
    /// </summary>
    /// <param name="labels">The labels to filter.</param>
    /// <param name="source">The <see cref="Did"/> a label must have been applied by.</param>
    /// <param name="uri">The URI a label must have been applied to.</param>
    /// <param name="matchCid"><see langword="true"/> if a label must also match <paramref name="cid"/>, otherwise <see langword="false"/>.</param>
    /// <param name="cid">The <see cref="Cid"/> a label must have been applied to, when <paramref name="matchCid"/> is <see langword="true"/>.</param>
    /// <returns>The distinct values of the matching labels, which is an empty, shared, list if no labels match.</returns>
    public static IReadOnlyList<string> Read(IReadOnlyCollection<Label> labels, Did source, string uri, bool matchCid = false, Cid? cid = null)
    {
        List<string>? values = null;
        HashSet<string>? seen = null;

        foreach (Label label in labels)
        {
            if (label.Source != source ||
                !string.Equals(label.Uri, uri, StringComparison.Ordinal) ||
                (matchCid && cid != label.Cid))
            {
                continue;
            }

            values ??= [];

            if (seen is not null)
            {
                if (seen.Add(label.Value))
                {
                    values.Add(label.Value);
                }
            }
            else if (!values.Contains(label.Value))
            {
                values.Add(label.Value);

                if (values.Count >= HashSetThreshold)
                {
                    seen = new HashSet<string>(values, StringComparer.Ordinal);
                }
            }
        }

        return values is null ? [] : values.AsReadOnly();
    }
}
