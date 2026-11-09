// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Host.MySQL;

/// <summary>Runs the canonical labeler with external MySQL persistence.</summary>
internal static class Program
{
    /// <summary>Runs the application or its provider-independent readiness probe.</summary>
    private static Task<int> Main(string[] args) =>
        LabelerApplicationExtensions.RunAsync(args, builder => builder.AddMySqlCanonicalLabelerStorage());
}
