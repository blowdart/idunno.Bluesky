// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// An embedded record with media.
    /// </summary>
    public sealed record EmbeddedRecordWithMediaView : EmbeddedRecordView
    {
        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedRecordWithMediaView"/>
        /// </summary>
        /// <param name="record">A view over the record.</param>
        /// <param name="media">The embedded media for the embedded record.</param>
        [JsonConstructor]
        internal EmbeddedRecordWithMediaView(View record, EmbeddedView media) : base(record)
        {
            Media = media;
        }

        /// <summary>
        /// Embedded media for the embedded record.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public EmbeddedView Media { get; init; }
    }
}
