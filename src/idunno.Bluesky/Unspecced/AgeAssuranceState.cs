// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Unspecced
{
    /// <summary>
    /// The current state of a user's age assurance.
    /// </summary>
    /// <param name="LastInitiatedAt">A <see cref="DateTimeOffset"/> when the age assurance process was last initiated, if any.</param>
    /// <param name="Status">The current <see cref="AgeAssuranceStatus"/> of the age assurance state.</param>
    public sealed record AgeAssuranceState(DateTimeOffset? LastInitiatedAt, AgeAssuranceStatus Status)
    {
    }
}
