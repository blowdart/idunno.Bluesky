// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Unspecced
{
    /// <summary>
    /// Encapsulates a tagged suggestion.
    /// </summary>
    /// <param name="Tag">The tag for the suggestion</param>
    /// <param name="SubjectType">The type of the subject. Current know values are actor and feed.</param>
    /// <param name="Subject">A <see cref="Uri"/> to the subject.</param>
    public sealed record Suggestion(string Tag, string SubjectType, Uri Subject)
    {
    }
}
