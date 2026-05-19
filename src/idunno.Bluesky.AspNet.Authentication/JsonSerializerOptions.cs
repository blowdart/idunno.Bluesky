// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.AspNet.Authentication;

internal static class JsonSerializerOptions
{
    static JsonSerializerOptions()
    {
        Options = AtProtoJsonSerializerOptions.Options;
        Options.TypeInfoResolverChain.Insert(0, BlueskyJsonSerializerOptions.Options.TypeInfoResolver!);
        Options.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
    }

    public static System.Text.Json.JsonSerializerOptions Options { get; }

}
