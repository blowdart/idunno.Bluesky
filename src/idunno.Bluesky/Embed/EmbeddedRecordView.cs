// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    public record EmbeddedRecordView : EmbeddedView
    {
        [JsonConstructor]
        public EmbeddedRecordView(View record) : base()
        {
            Record = record;
        }

        [JsonInclude]
        public View Record { get; init; }
    }
}
