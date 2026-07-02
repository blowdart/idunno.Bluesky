// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Gets the proxy to use with an <see cref="AtProtoHttpClient{T}"/> to access chat APIs.
    /// </summary>
    private const string ChatProxy = "did:web:api.bsky.chat#bsky_chat";
}