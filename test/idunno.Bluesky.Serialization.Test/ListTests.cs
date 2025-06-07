// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class ListTests
    {
        [Fact]
        public void BlueskyListDeserializesCorrectlyWithSourceGeneratedJsonContext()
        {
            string json = """
                {
                    "name": "Super Bean Fans",
                    "$type": "app.bsky.graph.list",
                    "purpose": "app.bsky.graph.defs#curatelist",
                    "createdAt": "2025-05-01T03:50:05.7760765+00:00",
                    "description": "People who realise the glory of Heinz Baked Beans.",
                    "descriptionFacets": []
                }
                """;

            BlueskyList? actual = JsonSerializer.Deserialize<BlueskyList>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(actual);
            Assert.Equal("Super Bean Fans", actual.Name);
            Assert.Equal(ListPurpose.CurateList, actual.Purpose);
        }
    }
}
