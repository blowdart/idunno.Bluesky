// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Unspecced.Model;

internal sealed record GetPostThreadV2Response(
#pragma warning disable BSKYUnspecced // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    [field: JsonRequired] ICollection<ThreadItem> Thread,
#pragma warning restore BSKYUnspecced // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    ThreadGateView? Threadgate,
    [field: JsonRequired] bool HasOtherReplies)
{
}
