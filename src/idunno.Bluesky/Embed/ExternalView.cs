// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    public record ExternalView
    {
        [JsonConstructor]
        public ExternalView(Uri uri, string title, string description, string thumb)
        {
            Uri = uri;
            Title = title;
            Description = description;

            Thumb = thumb;
        }

        [JsonInclude]
        [JsonRequired]
        public Uri Uri { get; init; }

        [JsonInclude]
        [JsonRequired]
        public string Title { get; init; }

        [JsonInclude]
        [JsonRequired]
        public string Description { get; init; }

        [JsonInclude]
        public string? Thumb { get; init; }
    }
}
