// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo;

/// <summary>
/// The result of a deleteRecord API call.
/// </summary>
/// <remarks>
/// <para>
///   The <see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/repo/deleteRecord.json">lexicon</see>
///   declares no required output properties, so a server may delete the record without reporting the commit it was
///   deleted in. The delete still succeeded, which is why the commit is carried in a result type rather than being the
///   result itself.
/// </para>
/// </remarks>
public sealed record DeleteResult
{
    /// <summary>
    /// Creates a new instance of <see cref="DeleteResult"/>.
    /// </summary>
    /// <param name="commit">The <see cref="Repo.Commit"/> the record was deleted in, if the server returned one.</param>
    public DeleteResult(Commit? commit)
    {
        Commit = commit;
    }

    /// <summary>
    /// Gets the <see cref="Repo.Commit"/> the record was deleted in, if the server returned one.
    /// </summary>
    public Commit? Commit { get; }
}
