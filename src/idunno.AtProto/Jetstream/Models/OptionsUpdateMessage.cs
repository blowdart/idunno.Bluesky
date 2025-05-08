// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream.Models
{
    internal sealed record OptionsUpdateMessage
    {
        [JsonInclude]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Cannot be static as it needs to be part of json serialization")]
        public string Type => "options_update";

        public required OptionsUpdatePayload Payload { get; init; }
    }
}
