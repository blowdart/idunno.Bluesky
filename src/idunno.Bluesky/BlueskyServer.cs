// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky;

/// <summary>
/// API wrappers for BlueskyAPIs.
/// </summary>
public static partial class BlueskyServer
{
    // See https://docs.bsky.app/blog/2025-protocol-roadmap-spring
    private const string AppViewProxy = "did:web:api.bsky.app#bsky_appview";

    /// <summary>
    /// Returns the entries in <paramref name="source"/> with any null entries removed, logging a warning for each batch of entries removed.
    /// </summary>
    /// <typeparam name="T">The type of the entries in <paramref name="source"/>.</typeparam>
    /// <param name="source">The collection returned by the API.</param>
    /// <param name="service">The <see cref="Uri"/> of the service the collection was returned by.</param>
    /// <param name="collection">The name of the collection property the entries were read from.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to log the removal of any null entries to.</param>
    /// <param name="caller">The name of the API wrapper the collection was returned to.</param>
    /// <returns>The entries in <paramref name="source"/> with any null entries removed.</returns>
    /// <remarks>
    /// <para>
    /// A collection property annotated as non-nullable only guarantees that the collection itself is not null. Neither
    /// <see cref="System.Text.Json.Serialization.JsonRequiredAttribute"/> nor <see cref="System.Text.Json.JsonSerializerOptions.RespectNullableAnnotations"/>
    /// apply to the element type of a collection, so a service which returns a null entry inside an otherwise well formed
    /// collection would otherwise hand that null straight through to the caller.
    /// </para>
    /// </remarks>
    private static List<T> WithoutNullEntries<T>(
        IEnumerable<T> source,
        Uri service,
        string collection,
        ILoggerFactory? loggerFactory,
        [CallerMemberName] string caller = "") where T : class
    {
        List<T> entries = [];
        int skipped = 0;

        foreach (T? entry in source)
        {
            if (entry is null)
            {
                skipped++;
            }
            else
            {
                entries.Add(entry);
            }
        }

        if (skipped != 0)
        {
            ILogger logger = loggerFactory?.CreateLogger(nameof(BlueskyServer)) ?? NullLogger.Instance;
            Logger.SkippedNullCollectionEntries(logger, caller, skipped, collection, service);
        }

        return entries;
    }
}