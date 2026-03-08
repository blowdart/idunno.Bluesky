// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test
{
    public class AccountStatusTests
    {
        [Fact]
        public void StatusDeserializesCorrectly()
        {
            string json = """
                {
                    "$type": "app.bsky.actor.status",
                    "status": "app.bsky.actor.status#live",
                    "embed": {
                        "$type": "app.bsky.embed.external",
                        "external": {
                            "uri": "https://twitch.tv/streamer",
                            "$type": "app.bsky.embed.external#external",
                            "title": "BeanOnBean",
                            "description": "Hot Bean on bean action"
                        }
                    },
                    "createdAt": "2026-03-08T03:47:25.4901851+00:00",
                    "durationMinutes": 60
                }
                """;

            Status? status = JsonSerializer.Deserialize<Status>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(status);
            Assert.Equal("app.bsky.actor.status#live", status!.AccountStatus);
            Assert.IsType<EmbeddedExternal>(status.Embed);
            var embeddedExternal = (EmbeddedExternal)status.Embed!;
            Assert.NotNull(embeddedExternal);
            Assert.NotNull(embeddedExternal.External);
            Assert.Equal(new Uri("https://twitch.tv/streamer"), embeddedExternal.External.Uri);
            Assert.Equal("BeanOnBean", embeddedExternal.External.Title);
            Assert.Equal("Hot Bean on bean action", embeddedExternal.External.Description);

            Assert.Equal(60, status.DurationMinutes);
            Assert.Equal(DateTimeOffset.Parse("2026-03-08T03:47:25.4901851+00:00"), status.CreatedAt);
        }

        [Fact]
        public void StatusDeserializesCorrectlyWithJustRequiredFields()
        {
            string json = """
                {
                    "$type": "app.bsky.actor.status",
                    "status": "app.bsky.actor.status#live",
                    "createdAt": "2026-03-08T03:47:25.4901851+00:00"
                }
                """;

            Status? status = JsonSerializer.Deserialize<Status>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(status);

            Assert.Equal("app.bsky.actor.status#live", status!.AccountStatus);
            Assert.Equal(DateTimeOffset.Parse("2026-03-08T03:47:25.4901851+00:00"), status.CreatedAt);
        }

    }
}
