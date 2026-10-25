// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Labeler.Administration;

namespace idunno.Bluesky.Labeler.Host;

/// <summary>Provides reflection-free metadata for the host's bootstrap receipt output.</summary>
[JsonSerializable(typeof(LabelAdministrationReceipt))]
internal sealed partial class HostJsonSerializerContext : JsonSerializerContext
{
}
