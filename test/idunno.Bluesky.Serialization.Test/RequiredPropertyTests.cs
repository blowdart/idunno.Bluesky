// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;
using idunno.Bluesky.Actor.Model;
using idunno.Bluesky.Chat.Group.Model;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed.Model;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Graph.Model;

namespace idunno.Bluesky.Serialization.Test;

public class RequiredPropertyTests
{
    [Fact]
    public void GetSuggestedUsersResponseRecommendationIdentifierIsAnnotatedAsNullable()
    {
        // recId is optional in app.bsky.unspecced.getSuggestedUsers. A missing field silently leaves a non-nullable
        // reference member null rather than throwing, so only the annotation, not a runtime value check, can catch
        // the member claiming a guarantee the wire format does not give.
        PropertyInfo property = typeof(Unspecced.Model.GetSuggestedUsersResponse)
            .GetProperty(nameof(Unspecced.Model.GetSuggestedUsersResponse.RecId))!;
        NullabilityInfo nullability = new NullabilityInfoContext().Create(property);

        Assert.Equal(NullabilityState.Nullable, nullability.ReadState);
    }

    [Fact]
    public void NoFieldInTheBlueskyAssemblyCarriesJsonRequired()
    {
        // [field: JsonRequired] on a positional record parameter lands on the compiler generated backing field,
        // where System.Text.Json ignores it, silently leaving the property optional. Only [property:] works.
        List<string> offenders = [];

        foreach (Type type in typeof(BlueskyServer).Assembly.GetTypes())
        {
            FieldInfo[] fields = type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttribute<JsonRequiredAttribute>() is not null)
                {
                    offenders.Add($"{type.FullName}.{field.Name}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData(typeof(SearchPostsResponse), """{"cursor":"x"}""")]
    [InlineData(typeof(SearchActorsTypeAheadResponse), """{}""")]
    [InlineData(typeof(RelationshipMap), """{"actor":"did:plc:abc123abc123abc123abc123"}""")]
    [InlineData(typeof(LabelersPreference), """{"$type":"app.bsky.actor.defs#labelersPref"}""")]
    [InlineData(typeof(KnownFollowers), """{"count":1}""")]
    [InlineData(typeof(ListJoinRequestsResponse), """{"cursor":"x"}""")]
    [InlineData(typeof(SearchStarterPacksResponse), """{"cursor":"x"}""")]
    [InlineData(typeof(SearchStarterPacksV2Response), """{"cursor":"x"}""")]
    [InlineData(typeof(EmbeddedImages), """{}""")]
    public void OmittingARequiredCollectionThrowsJsonException(Type type, string json)
    {
        // A missing property is not covered by RespectNullableAnnotations, which only rejects an explicit null,
        // so without [JsonRequired] these deserialize successfully with a null member.
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize(json, type, BlueskyJsonSerializerOptions.Options));
    }

    [Fact]
    public void EmbeddedImagesDeserializesAnEmptyImagesArray()
    {
        // app.bsky.embed.images sets no minimum, so an empty array is a valid payload.
        EmbeddedImages? actual = JsonSerializer.Deserialize<EmbeddedImages>(
            """{"images":[]}""", options: BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(actual);
        Assert.Empty(actual.Images);
    }

    [Fact]
    public void EmbeddedImagesStillRejectsAnEmptyCollectionWhenConstructedDirectly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmbeddedImages([]));
    }
}
