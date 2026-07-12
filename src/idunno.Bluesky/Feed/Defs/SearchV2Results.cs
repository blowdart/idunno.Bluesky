// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A collection of <see cref="PostView"/>s returned from a search query.
/// </summary>
public sealed class SearchV2Results : PagedViewReadOnlyCollection<PostView>
{
    internal SearchV2Results(IList<PostView> results, int? totalHits, string? cursor = null, IList<string>? detectedQueryLanguages = null) :
        base(results, cursor)
    {
        TotalHits = totalHits;
        DetectedQueryLanguages = detectedQueryLanguages != null ? new List<string>(detectedQueryLanguages).AsReadOnly() : null;
    }

    /// <summary>
    /// Gets the estimated total number of matching hits. May be rounded or truncated.
    /// </summary>
    public int? TotalHits { get; init; }

    /// <summary>
    /// Gets the query languages detected for CJK, Thai, or Arabic text. Empty or omitted for other scripts.
    /// </summary>
    public IReadOnlyCollection<string>? DetectedQueryLanguages { get; init; }
}