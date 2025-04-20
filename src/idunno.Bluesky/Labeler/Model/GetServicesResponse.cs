// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Model
{
    internal sealed record GetServicesResponse
    {
        public IEnumerable<LabelerView>? Views { get; set; }
    }
}
