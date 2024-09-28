// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Contains link information for a blob.
    /// </summary>
    public record BlobReference
    {
        /// <summary>
        /// Creates a new instance of <see cref="BlobReference"/>.
        /// </summary>
        /// <param name="link">The link to a blob.</param>
        [JsonConstructor]
        public BlobReference(string link)
        {
            Link = link;
        }

        /// <summary>
        /// Gets the link to the blob.
        /// </summary>
        /// <value>The link to the blob.</value>
        [JsonInclude]
        [JsonPropertyName("$link")]
        public string Link { get; internal set; }
    }
}
