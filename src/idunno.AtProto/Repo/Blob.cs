// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Represents a blob contained in a repo.
    ///
    /// Typically this is used for image uploads for posts with embedded images or video uploads for posts with embedded videos.
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

        /// <summary>
        /// The json type data for a blob object
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("$type")]
        public static string Type => "blob";

        /// <summary>
        /// The reference to the blob.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ref")]
        public BlobReference Reference { get; internal set;}

        /// <summary>
        /// The MIME type for the blob.
        /// </summary>
        [JsonInclude]
        public string MimeType { get; internal set;}

        /// <summary>
        /// The size of the blob, in bytes.
        /// </summary>
        [JsonInclude]
        public int Size { get; internal set;}
    }
}
