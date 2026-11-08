// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Host.PostgreSQL;

/// <summary>Runs the canonical labeler with external PostgreSQL persistence.</summary>
internal static class Program
{
    /// <summary>Runs the application or its provider-independent readiness probe.</summary>
    private static Task<int> Main(string[] args) =>
        LabelerApplicationExtensions.RunAsync(args, builder => builder.AddPostgreSqlCanonicalLabelerStorage());
}
