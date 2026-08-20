// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Serialization.Test;

public class ContentVisibilityDeclarationTests
{
    [Fact]
    public void ContentVisibilityDeclarationWithHideFromAlgorithmicRecommendationsSetToTrueDeserializesCorrectly()
    {
        string json = """
            {
              "$type": "app.bsky.actor.contentVisibilityDeclaration",
              "hideFromAlgorithmicRecommendations": true
            }
            """;

        ContentVisibilityDeclaration? result = JsonSerializer.Deserialize<ContentVisibilityDeclaration>(json, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(result);
        Assert.True(result.HideFromAlgorithmicRecommendations);
    }

    [Fact]
    public void ContentVisibilityDeclarationHideFromAlgorithmicRecommendationsSetToFalseDeserializesCorrectly()
    {
        string json = """
            {
              "$type": "app.bsky.actor.contentVisibilityDeclaration",
              "hideFromAlgorithmicRecommendations": false
            }
            """;

        ContentVisibilityDeclaration? result = JsonSerializer.Deserialize<ContentVisibilityDeclaration>(json, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(result);
        Assert.False(result.HideFromAlgorithmicRecommendations);
    }

    [Fact]
    public void ContentVisibilityDeclarationHideFromAlgorithmicRecommendationsMissingPropertyThrowsJsonException()
    {
        string json = """
            {
              "$type": "app.bsky.actor.contentVisibilityDeclaration"
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ContentVisibilityDeclaration>(json, BlueskyJsonSerializerOptions.Options));
    }

    [Fact]
    public void ContentVisibilityDeclarationHideFromAlgorithmicRecommendationsSerializesCorrectly()
    {
        ContentVisibilityDeclaration declaration = new (HideFromAlgorithmicRecommendations: true);

        string json = JsonSerializer.Serialize(declaration, BlueskyJsonSerializerOptions.Options);

        Assert.Equal("""
            {"$type":"app.bsky.actor.contentVisibilityDeclaration","hideFromAlgorithmicRecommendations":true}
            """, json);
    }
}
