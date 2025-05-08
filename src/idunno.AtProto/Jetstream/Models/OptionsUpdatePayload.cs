// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Jetstream.Models
{
    internal sealed record OptionsUpdatePayload
    {
        public Nsid[]? WantedCollections { get; set; }

        public Did[]? WantedDIDs { get; set; }

        public required int MaxMessageSizeBytes { get; init; }
    }
}
