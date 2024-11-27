// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// Encapsulates a view over an individual item in a list.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed record ListItemView : View
    {
        /// <summary>
        /// Creates a new instance of <see cref="ListItemView"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of list item.</param>
        /// <param name="subject">A <see cref="ProfileView"/> of the actor the list item refers to.</param>
        public ListItemView(AtUri uri, ProfileView subject)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(subject);

            Uri = uri;
            Subject = subject;
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the list item.
        /// </summary>
        [JsonRequired]
        public AtUri Uri { get; init; }

        /// <summary>
        /// Gets a <see cref="ProfileView"/> of the actor the list item refers to.
        /// </summary>
        [JsonRequired]
        public ProfileView Subject { get; init; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"{{{Subject.Handle} => {Uri}}}";
            }
        }
    }
}
