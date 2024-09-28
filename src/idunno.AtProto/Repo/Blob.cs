// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Represents a blob contained in a repo.
    ///
    /// Typically this is used for image uploads for posts with embedded images.
    /// </summary>
    public record Blob
    {
        /// <summary>
        /// Creates a new instance of <see cref="Blob"/>.
        /// </summary>
        /// <param name="reference">Reference information for linking to the blob in a post.</param>
        /// <param name="mimeType">The mime type of the blob.</param>
        /// <param name="size">The size of the blob in bytes.</param>
        [JsonConstructor]
        public Blob(BlobReference reference, string mimeType, int size)
        {
            Reference = reference;
            MimeType = mimeType;
            Size = size;
        }

        [JsonInclude]
        [JsonPropertyName("$type")]
        public string Type { get; internal set; } = "blob";

        [JsonInclude]
        [JsonPropertyName("ref")]
        public BlobReference Reference { get; internal set;}

        [JsonInclude]
        public string MimeType { get; internal set;}

        [JsonInclude]
        public int Size { get; internal set;}
    }
}
