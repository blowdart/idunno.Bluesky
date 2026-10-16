// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Test;

public class XrpcEndpointNameTests
{
    [Theory]
    [InlineData("/xrpc/app.bsky.feed.getTimeline", "app.bsky.feed.getTimeline")]
    [InlineData("/xrpc/app.bsky.feed.getTimeline?limit=50&cursor=abc", "app.bsky.feed.getTimeline")]
    [InlineData("/xrpc/com.atproto.sync.getBlob/extra", "com.atproto.sync.getBlob")]
    [InlineData("xrpc/com.atproto.server.getSession", "")]
    [InlineData("https://bsky.social/xrpc/com.atproto.server.getSession?x=1", "")]
    [InlineData("/xrpc/", "")]
    [InlineData("/.well-known/did.json", "")]
    [InlineData("", "")]
    public void GetXrpcEndpointNameReturnsTheMethodName(string endpoint, string expected)
    {
        Assert.Equal(expected, AtProtoHttpClient<object>.GetXrpcEndpointName(endpoint));
    }
}
