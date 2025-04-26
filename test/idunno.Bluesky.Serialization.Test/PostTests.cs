// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class PostTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void SimplePostDeserializesCorrectlyWithSourceGeneratedJsonContext()
        {
            string json = """
                {
                    "$type": "app.bsky.feed.post",
                    "createdAt": "2025-04-25T17:25:46.3164586+00:00",
                    "langs": [
                        "en"
                    ],
                    "text": "Post text"
                }
                """;

            Post? post = JsonSerializer.Deserialize<Post>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(post);
            Assert.Equal("Post text", post.Text);
            Assert.NotNull(post.Langs);
            Assert.Single(post.Langs);
            Assert.Equal("en", post.Langs.ElementAt(0));
            Assert.Equal(DateTimeOffset.Parse("2025-04-25T17:25:46.3164586+00:00"), post.CreatedAt);
        }

        [Fact]
        public void SimplePostDeserializesCorrectlyWithoutSourceGeneratedJsonContext()
        {
            string json = """
                {
                    "$type": "app.bsky.feed.post",
                    "createdAt": "2025-04-25T17:25:46.3164586+00:00",
                    "langs": [
                        "en"
                    ],
                    "text": "Post text"
                }
                """;

            Post? post = JsonSerializer.Deserialize<Post>(json, _jsonSerializerOptions);

            Assert.NotNull(post);
            Assert.Equal("Post text", post.Text);
            Assert.NotNull(post.Langs);
            Assert.Single(post.Langs);
            Assert.Equal("en", post.Langs.ElementAt(0));
            Assert.Equal(DateTimeOffset.Parse("2025-04-25T17:25:46.3164586+00:00"), post.CreatedAt);
        }

        [Fact]
        public void SimplePostDeserializesCorrectlyWithJavascriptDateTimeAndSourceGeneratedJsonContext()
        {
            string json = """
                {
                    "$type": "app.bsky.feed.post",
                    "createdAt": "2023-08-07T05:49:39.417839Z",
                    "langs": [
                        "en"
                    ],
                    "text": "Post text"
                }
                """;

            Post? post = JsonSerializer.Deserialize<Post>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(post);
            Assert.Equal("Post text", post.Text);
            Assert.NotNull(post.Langs);
            Assert.Single(post.Langs);
            Assert.Equal("en", post.Langs.ElementAt(0));
            Assert.Equal(DateTimeOffset.Parse("2023-08-07T05:49:39.417839Z"), post.CreatedAt);
        }
    }
}
