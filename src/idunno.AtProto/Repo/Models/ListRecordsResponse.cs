// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates the response of a list records request.
    /// </summary>
    internal sealed record ListRecordsResponse
    {
        /// <summary>
        /// Creates a new instance of <see cref="ListRecordsResponse"/>.
        /// </summary>
        /// <param name="records">The records returned.</param>
        /// <param name="cursor">An optional cursor returned by the underlying API pagination.</param>
        [JsonConstructor]
        public ListRecordsResponse(ICollection<JsonObject> records, string? cursor)
        {
            Records = records;
            Cursor = cursor;
        }

        /// <summary>
        /// The list of records returned.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ICollection<JsonObject> Records { get; set; }

        /// <summary>
        /// An optional cursor returned by the underlying API pagination.
        /// </summary>
        [JsonInclude]
        public string? Cursor { get; set; }
    }
}
