// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// A ReadOnly collection of <see cref="ProfileView"/>s for the <see cref="Subject"/>'s follows.
    /// </summary>
    public class Follows : PagedViewReadOnlyCollection<ProfileView>
    {
        /// <summary>
        /// Creates a new instance of <see cref="Follows"/>.
        /// </summary>
        /// <param name="subject">The <see cref="ProfileView"/> of the subject.</param>
        /// <param name="follows">A list of the follows' <see cref="ProfileView"/>s.</param>
        /// <param name="cursor">An optional cursor for API pagination.</param>
        public Follows(ProfileView? subject, IList<ProfileView> follows, string? cursor) : base(follows, cursor)
        {
            Subject = subject;
        }

        /// <summary>
        /// The <see cref="ProfileView"/> of the subject whose follows are the contents of the collection.
        /// </summary>
        public ProfileView? Subject { get; init; }
    }
}
