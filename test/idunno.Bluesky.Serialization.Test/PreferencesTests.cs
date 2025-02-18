// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Actor.Model;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class PreferencesTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void ThreadViewPreferenceSerializesToJson()
        {
            var threadViewPreference = new ThreadViewPreference(prioritizeFollowedUsers: true);

            string threadViewPreferenceAsJson = JsonSerializer.Serialize(threadViewPreference, _jsonSerializerOptions);

            Assert.NotNull(threadViewPreferenceAsJson);
        }

        [Fact]
        public void PutPreferencesRequestSerializesToJson()
        {
            var threadViewPreference = new ThreadViewPreference(prioritizeFollowedUsers: true);

            var putPreferencesRequest = new PutPreferencesRequest(new Preferences([threadViewPreference]));

            var putPreferencesRequestAsJson = JsonSerializer.Serialize(putPreferencesRequest, _jsonSerializerOptions);

            Assert.NotNull(putPreferencesRequestAsJson);
        }
    }
}
