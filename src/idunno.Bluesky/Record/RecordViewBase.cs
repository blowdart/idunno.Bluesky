// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Base class for record views. Used for json polymorphic deserialization.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    public record RecordViewBase : View
    {
    }
}
