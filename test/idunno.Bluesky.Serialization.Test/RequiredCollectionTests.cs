// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Actor.Model;
using idunno.Bluesky.Bookmarks.Model;
using idunno.Bluesky.Feed.Model;
using idunno.Bluesky.Notifications.Model;
using idunno.Bluesky.Unspecced.Model;

namespace idunno.Bluesky.Serialization.Test;

/// <summary>
/// Paged responses whose collection members are declared non-nullable used to dereference null and throw
/// <see cref="NullReferenceException"/> or <see cref="ArgumentNullException"/> out of the client when a server
/// omitted the collection or sent an explicit null. <c>[JsonRequired]</c> rejects the omitted case and
/// <see cref="JsonSerializerOptions.RespectNullableAnnotations"/> rejects the explicit null case, so both now
/// surface as a <see cref="JsonException"/> the client converts into a failed result.
/// </summary>
public class RequiredCollectionTests
{
    private static JsonSerializerOptions Options => BlueskyJsonSerializerOptions.Options;

    [Fact]
    public void RespectNullableAnnotationsIsEnabled()
    {
        Assert.True(Options.RespectNullableAnnotations);
    }

    public static TheoryData<string, string> OmittedCollections => new()
    {
        { nameof(ListNotificationsResponse), """{"seenAt":"2024-01-01T00:00:00+00:00"}""" },
        { nameof(SearchActorsResponse), """{"cursor":"next"}""" },
        { nameof(GetSuggestionsResponse), """{"cursor":"next"}""" },
        { nameof(GetProfilesResponse), """{}""" },
        { nameof(GetBookmarksResponse), """{"cursor":"next"}""" },
        { nameof(SearchPostsV2Response), """{"cursor":"next"}""" },
        { nameof(GetTrendingTopicsResponse), """{"suggested":[]}""" },
    };

    public static TheoryData<string, string> NullCollections => new()
    {
        { nameof(ListNotificationsResponse), """{"notifications":null,"seenAt":"2024-01-01T00:00:00+00:00"}""" },
        { nameof(SearchActorsResponse), """{"actors":null}""" },
        { nameof(GetSuggestionsResponse), """{"actors":null}""" },
        { nameof(GetProfilesResponse), """{"profiles":null}""" },
        { nameof(GetBookmarksResponse), """{"bookmarks":null}""" },
        { nameof(SearchPostsV2Response), """{"posts":null}""" },
        { nameof(GetTrendingTopicsResponse), """{"topics":null,"suggested":[]}""" },
    };

    public static TheoryData<string, string> PopulatedCollections => new()
    {
        { nameof(ListNotificationsResponse), """{"notifications":[],"seenAt":"2024-01-01T00:00:00+00:00"}""" },
        { nameof(SearchActorsResponse), """{"actors":[]}""" },
        { nameof(GetSuggestionsResponse), """{"actors":[]}""" },
        { nameof(GetProfilesResponse), """{"profiles":[]}""" },
        { nameof(GetBookmarksResponse), """{"bookmarks":[]}""" },
        { nameof(SearchPostsV2Response), """{"posts":[]}""" },
        { nameof(GetTrendingTopicsResponse), """{"topics":[],"suggested":[]}""" },
    };

    [Theory]
    [MemberData(nameof(OmittedCollections))]
    public void OmittedRequiredCollectionThrowsJsonException(string responseType, string json)
    {
        Assert.Throws<JsonException>(() => Deserialize(responseType, json));
    }

    [Theory]
    [MemberData(nameof(NullCollections))]
    public void NullRequiredCollectionThrowsJsonException(string responseType, string json)
    {
        Assert.Throws<JsonException>(() => Deserialize(responseType, json));
    }

    [Theory]
    [MemberData(nameof(PopulatedCollections))]
    public void PopulatedCollectionDeserializes(string responseType, string json)
    {
        Assert.NotNull(Deserialize(responseType, json));
    }

    [Fact]
    public void OmittedTrendingTopicsSuggestedThrowsJsonException()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<GetTrendingTopicsResponse>("""{"topics":[]}""", Options));
    }

    [Fact]
    public void NullTrendingTopicsSuggestedThrowsJsonException()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<GetTrendingTopicsResponse>("""{"topics":[],"suggested":null}""", Options));
    }

    private static object? Deserialize(string responseType, string json) => responseType switch
    {
        nameof(ListNotificationsResponse) => JsonSerializer.Deserialize<ListNotificationsResponse>(json, Options),
        nameof(SearchActorsResponse) => JsonSerializer.Deserialize<SearchActorsResponse>(json, Options),
        nameof(GetSuggestionsResponse) => JsonSerializer.Deserialize<GetSuggestionsResponse>(json, Options),
        nameof(GetProfilesResponse) => JsonSerializer.Deserialize<GetProfilesResponse>(json, Options),
        nameof(GetBookmarksResponse) => JsonSerializer.Deserialize<GetBookmarksResponse>(json, Options),
        nameof(SearchPostsV2Response) => JsonSerializer.Deserialize<SearchPostsV2Response>(json, Options),
        nameof(GetTrendingTopicsResponse) => JsonSerializer.Deserialize<GetTrendingTopicsResponse>(json, Options),
        _ => throw new ArgumentOutOfRangeException(nameof(responseType), responseType, "Unknown response type.")
    };
}
