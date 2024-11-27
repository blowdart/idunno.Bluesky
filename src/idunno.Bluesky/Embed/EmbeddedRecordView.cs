// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Encapsulates an embedded view over a record.
    /// </summary>
    public record EmbeddedRecordView : EmbeddedView
    {
        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedRecordView"/>
        /// </summary>
        /// <param name="record">The view over the record.</param>
        [JsonConstructor]
        public EmbeddedRecordView(View record) : base()
        {
            Record = record;
        }

        /// <summary>
        /// Gets a view over the record.
        /// </summary>
        [JsonInclude]
        public View Record { get; init; }
    }
}
