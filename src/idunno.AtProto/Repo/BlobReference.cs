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
        /// <param name="link">The <see cref="Cid">content identifier</see> of the blob.</param>
        [JsonConstructor]
        public BlobReference(Cid link)
        {
            ArgumentNullException.ThrowIfNull(link);
            Link = link;
        }

        /// <summary>
        /// Gets the <see cref="Cid">content identifier</see> of the blob.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("$link")]
        [JsonRequired]
        public Cid Link { get; init; }
    }
}
