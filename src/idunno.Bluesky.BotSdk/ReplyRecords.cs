// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.Bluesky.BotSdk;

/// <summary>Checks complete reply equality and recognizes the pinned SDK's archive-generation failures.</summary>
internal static class ReplyRecords
{
    /// <summary>Compares every serialized field, including extensions, before accepting an existing fixed-key reply.</summary>
    /// <param name="actual">The remotely retrieved post.</param>
    /// <param name="expected">The complete durably saved reply.</param>
    /// <returns><see langword="true"/> if the records agree; otherwise, <see langword="false"/>.</returns>
    public static bool SameRecord(Post actual, Post expected) =>
        JsonElement.DeepEquals(
            JsonSerializer.SerializeToElement(actual, BotJson.Post),
            JsonSerializer.SerializeToElement(expected, BotJson.Post));

    /// <summary>Recognizes archive changes that require bounded re-enumeration rather than malformed-data skipping.</summary>
    /// <param name="exception">The archive read failure.</param>
    /// <returns><see langword="true"/> for a known generation mismatch; otherwise, <see langword="false"/>.</returns>
    /// <remarks><para>These exact messages are specific to the pinned SDK build and must be reviewed on upgrades.</para></remarks>
    public static bool IsGenerationMismatch(InvalidDataException exception) =>
        exception.Message.StartsWith("Archive download ETag mismatch for segment '", StringComparison.Ordinal) ||
        exception.Message == "The archive server did not resume the expected segment generation.";
}
