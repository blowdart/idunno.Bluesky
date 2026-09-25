// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Serialization.Test;

public class ViewerStateTests
{
    [Fact]
    public void ViewerStateTestDeserializesCorrectly()
    {
        string json = """
            {
            
                "muted": false,
                "mutedOnlyReposts": false,
                "mutedOnlyQuoteposts": false,
                "blockedBy": false
            }
            """;

        ViewerState? viewerState = JsonSerializer.Deserialize<ViewerState>(json, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(viewerState);
        Assert.False(viewerState.Muted);
        Assert.False(viewerState.MutedOnlyReposts);
        Assert.False(viewerState.MutedOnlyQuotePosts);
        Assert.False(viewerState.BlockedBy);
    }

    [Fact]
    public void EmptyViewerStateTestDeserializesCorrectlyAndSetsPropertiesToFalseNotNull()
    {
        string json = """
            {
            }
            """;

        ViewerState? viewerState = JsonSerializer.Deserialize<ViewerState>(json, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(viewerState);
        Assert.False(viewerState.Muted);
        Assert.False(viewerState.MutedOnlyReposts);
        Assert.False(viewerState.MutedOnlyQuotePosts);
        Assert.False(viewerState.BlockedBy);
    }

    [Fact]
    public void ViewerStateTestDeserializesCorrectlyWithMutedOnlyPropertiesSet()
    {
        string json = """
            {
            
                "muted": false,
                "mutedOnlyReposts": true,
                "mutedOnlyQuoteposts": true,
                "blockedBy": false
            }
            """;

        ViewerState? viewerState = JsonSerializer.Deserialize<ViewerState>(json, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(viewerState);
        Assert.False(viewerState.Muted);
        Assert.True(viewerState.MutedOnlyReposts);
        Assert.True(viewerState.MutedOnlyQuotePosts);
        Assert.False(viewerState.BlockedBy);
    }

}
