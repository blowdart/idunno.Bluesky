// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Graph;

/// <summary>
/// A ReadOnly collection of the <see cref="ProfileView"/>s of the followers of <see cref="Subject"/>.
/// </summary>
/// <remarks>
/// <para>Creates a new instance of <see cref="Followers"/>.</para>
/// </remarks>
/// <param name="subject">The <see cref="ProfileView"/> of the subject the followers are following.</param>
/// <param name="followers">A list of the followers' <see cref="ProfileView"/>s.</param>
/// <param name="cursor">An optional cursor for API pagination.</param>
public class Followers(ProfileView? subject, IList<ProfileView> followers, string? cursor) : PagedViewReadOnlyCollection<ProfileView>([.. followers], cursor)
{

    /// <summary>
    /// The <see cref="ProfileView"/> of the subject the followers are following.
    /// </summary>
    public ProfileView? Subject { get; init; } = subject;
}
