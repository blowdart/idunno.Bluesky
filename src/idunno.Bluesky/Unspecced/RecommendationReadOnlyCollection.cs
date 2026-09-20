// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace idunno.Bluesky.Unspecced;

/// <summary>
/// A list of <typeparamref name="T"/>, with an optional identifier for the recommendation which produced it.
/// </summary>
/// <typeparam name="T">The type of items the list contains.</typeparam>
/// <param name="list">The list to create this instance of <see cref="RecommendationReadOnlyCollection{T}"/> from.</param>
/// <param name="recommendationId">An optional identifier for the recommendation which produced <paramref name="list"/>.</param>
[Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
public class RecommendationReadOnlyCollection<T>(IList<T> list, string? recommendationId) : ReadOnlyCollection<T>([.. list])
{
    /// <summary>
    /// Creates a new instance of <see cref="RecommendationReadOnlyCollection{T}"/> with an empty list and no recommendation identifier.
    /// </summary>
    public RecommendationReadOnlyCollection() : this([], null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="RecommendationReadOnlyCollection{T}"/>.
    /// </summary>
    /// <param name="collection">A collection of <typeparamref name="T"/> to create this instance of <see cref="RecommendationReadOnlyCollection{T}"/> from.</param>
    /// <param name="recommendationId">An optional identifier for the recommendation which produced <paramref name="collection"/>.</param>
    public RecommendationReadOnlyCollection(ICollection<T> collection, string? recommendationId) : this([.. collection], recommendationId)
    {
    }

    /// <summary>
    /// An optional identifier for the recommendation which produced this collection, to be used when submitting recommendation events.
    /// </summary>
    public string? RecommendationId { get; } = recommendationId;
}
