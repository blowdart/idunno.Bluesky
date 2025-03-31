// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;

namespace idunno.Bluesky
{
    public partial class BlueskyServer
    {
        internal static JsonSerializerOptions BlueskyJsonSerializerOptions { get; } = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(SourceGenerationContext.Default);
    }
}
