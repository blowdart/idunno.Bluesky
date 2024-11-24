// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.Bluesky.Notifications.Model;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class NotificationsTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void EmptyListNotificationsResponseDeserializationsFromJson()
        {
            string jsonString = File.ReadAllText(@"json\empty_listNotificationsResponse.json");

            ListNotificationsResponse? notification = JsonSerializer.Deserialize<ListNotificationsResponse>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(notification);

            Assert.Empty(notification.Notifications);
            Assert.Equal("cursor", notification.Cursor);
            Assert.False(notification.Priority);
            Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), notification.SeenAt);
        }

        [Fact]
        public void ListNotificationsResponseDeserializationsFromJson()
        {
            string jsonString = File.ReadAllText(@"json\listNotificationsResponse.json");

            ListNotificationsResponse? notification = JsonSerializer.Deserialize<ListNotificationsResponse>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(notification);

            Assert.NotEmpty(notification.Notifications);
            Assert.Single(notification.Notifications);
            Assert.Equal("cursor", notification.Cursor);
            Assert.False(notification.Priority);
            Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), notification.SeenAt);
        }
    }
}
