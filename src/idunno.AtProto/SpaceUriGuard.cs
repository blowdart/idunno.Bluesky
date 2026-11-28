// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto;

/// <summary>
/// Creates the exception thrown when a space URI is passed to an operation on a public repository.
/// </summary>
/// <remarks>
/// <para>
/// A space URI, which begins <c>at:space/</c>, addresses permissioned data in a per-author space repository. Its
/// <see cref="AtUri.Authority"/> is the space authority, not a repository, and its <see cref="AtUri.Collection"/> and
/// <see cref="AtUri.RecordKey"/> are relative to the space. Public repository operations must reject space URIs before
/// making network calls or changes, so that a record in a space is never mistaken for a public record with the same
/// collection and record key.
/// </para>
/// </remarks>
internal static class SpaceUriGuard
{
    /// <summary>
    /// Creates an <see cref="ArgumentException"/> reporting that <paramref name="uri"/> is a space URI, which cannot be used with a public repository.
    /// </summary>
    /// <param name="uri">The space URI that was passed.</param>
    /// <param name="paramName">The name of the parameter that held <paramref name="uri"/>.</param>
    /// <returns>An <see cref="ArgumentException"/> to throw.</returns>
    internal static ArgumentException NotSupported(AtUri uri, string paramName) =>
        new($"{uri} is a space URI, which cannot be used with a public repository.", paramName);
}
