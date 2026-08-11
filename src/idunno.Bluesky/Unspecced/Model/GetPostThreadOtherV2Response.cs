// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Unspecced.Model;

internal record GetPostThreadOtherV2Response([field: JsonRequired] IReadOnlyCollection<ThreadItem> Thread)
{
}
