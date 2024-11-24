// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using idunno.Bluesky.Notifications;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class NotificationTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void NotificationDeserializationsFromJson()
        {
            string jsonString = File.ReadAllText(Path.Combine("json", "notification.json"));

            Notification? notification = JsonSerializer.Deserialize<Notification>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(notification);
        }
    }
}
