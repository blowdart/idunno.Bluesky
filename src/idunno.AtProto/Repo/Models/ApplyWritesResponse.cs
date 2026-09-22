// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo.Models;

/// <remarks>
/// <para>
///   The <see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/repo/applyWrites.json">lexicon</see>
///   declares no required output properties, so a server is free to return neither the commit nor the results.
/// </para>
/// </remarks>
internal sealed record ApplyWritesResponse(Commit? Commit, IReadOnlyCollection<ApplyWritesResponseBase>? Results)
{
}
