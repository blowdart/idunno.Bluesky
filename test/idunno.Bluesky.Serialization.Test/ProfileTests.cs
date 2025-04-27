// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class ProfileTests
    {
        [Fact]
        public void ProfileRecordDeserializesCorrectlyWithSourceGeneratedJsonContext()
        {
            string json = """
                {
                    "$type": "app.bsky.actor.profile",
                    "avatar": {
                        "$type": "blob",
                        "ref": {
                            "$link": "bafkreib6vy272khka47dedwort35c4zxl52j4ddijh2e7m23laaebotytq"
                        },
                        "mimeType": "image/jpeg",
                        "size": 357802
                    },
                    "banner": {
                        "$type": "blob",
                        "ref": {
                            "$link": "bafkreibn6toizt6ma2woo2etv2ttqtitnmutwbtqq6zzlnojjxu5zfql7m"
                        },
                        "mimeType": "image/jpeg",
                        "size": 246524
                    },
                    "labels": {
                        "$type": "com.atproto.label.defs#selfLabels",
                        "values": []
                    },
                    "pinnedPost": {
                        "cid": "bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une",
                        "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ldegderd6s2e"
                    },
                    "description": "Updated on 12/22/24 04:13:44",
                    "displayName": "Test Bot"
                }
                """;

            ProfileRecordValue? actual = JsonSerializer.Deserialize<ProfileRecordValue>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(actual);
        }
    }
}
