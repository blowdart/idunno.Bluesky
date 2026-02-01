// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Feed.Gates
{
    /// <summary>
    /// Thread gate rule specifying that replies are allowed from actors on a list.
    /// </summary>
    public record ListRule : ThreadGateRule
    {
        /// <summary>
        /// Creates a new instance of <see cref="ListRule"/>
        /// </summary>
        /// <param name="list">The <see cref="AtUri"/> of the list of actors that will be able to reply.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/> is <see langword="null"/>.</exception>
        public ListRule(AtUri list)
        {
            ArgumentNullException.ThrowIfNull(list);
            List = list;
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the list of actors that will be able to reply.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public AtUri List { get; init; }
    }
}
