// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// Possible purposes of a list
    /// </summary>
    /// <remarks>
    ///<para>See https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/graph/defs.json</para>
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ListPurpose
    {
        /// <summary>
        /// The thread gate is due to a moderation list.
        /// </summary>
        [JsonStringEnumMemberName("app.bsky.graph.defs#modlist")]
        ModList,

        /// <summary>
        /// The thread gate is due to a curation list.
        /// </summary>
        [JsonStringEnumMemberName("app.bsky.graph.defs#curatelist")]
        CurateList,

        /// <summary>
        /// The thread gate is due to a reference list, such as an actor within a starter pack.
        /// </summary>
        [JsonStringEnumMemberName("app.bsky.graph.defs#referencelist")]
        ReferenceList
    }
}
