// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Graph
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed record ListItemView : View
    {
        public ListItemView(AtUri uri, ProfileView subject)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(subject);

            Uri = uri;
            Subject = subject;
        }

        [JsonRequired]
        public AtUri Uri { get; init; }

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
