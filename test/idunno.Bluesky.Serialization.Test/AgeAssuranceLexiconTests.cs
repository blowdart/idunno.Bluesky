// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.Bluesky.AgeAssurance;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class AgeAssuranceLexiconTests
{
    [Fact]
    public void ConfigDeserializesEveryKnownRegionRule()
    {
        const string json =
            """
            {
              "regions": [
                {
                  "countryCode": "US",
                  "regionCode": "US-CA",
                  "platforms": ["web", "ios", "android", "futurePlatform"],
                  "minAccessAge": 13,
                  "additionalVerificationMethods": ["device", "futureMethod"],
                  "rules": [
                    {"$type": "app.bsky.ageassurance.defs#configRegionRuleDefault", "access": "full"},
                    {"$type": "app.bsky.ageassurance.defs#configRegionRuleIfDeclaredOverAge", "age": 18, "access": "safe"},
                    {"$type": "app.bsky.ageassurance.defs#configRegionRuleIfDeclaredUnderAge", "age": 13, "access": "none"},
                    {"$type": "app.bsky.ageassurance.defs#configRegionRuleIfAssuredOverAge", "age": 18, "access": "full"},
                    {"$type": "app.bsky.ageassurance.defs#configRegionRuleIfAssuredUnderAge", "age": 13, "access": "safe"},
                    {"$type": "app.bsky.ageassurance.defs#configRegionRuleIfAccountNewerThan", "date": "2025-01-01T00:00:00Z", "access": "none"},
                    {"$type": "app.bsky.ageassurance.defs#configRegionRuleIfAccountOlderThan", "date": "2020-01-01T00:00:00Z", "access": "full"}
                  ]
                }
              ]
            }
            """;

        Config? actual = JsonSerializer.Deserialize<Config>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(actual);
        ConfigRegion region = Assert.Single(actual.Regions);
        Assert.Contains(AgeAssurancePlatform.Unknown, region.Platforms!);
        Assert.Contains(AdditionalVerificationMethod.Unknown, region.AdditionalVerificationMethods!);
        Assert.Collection(
            region.Rules,
            rule => Assert.IsType<ConfigRegionRuleDefault>(rule),
            rule => Assert.IsType<ConfigRegionRuleIfDeclaredOverAge>(rule),
            rule => Assert.IsType<ConfigRegionRuleIfDeclaredUnderAge>(rule),
            rule => Assert.IsType<ConfigRegionRuleIfAssuredOverAge>(rule),
            rule => Assert.IsType<ConfigRegionRuleIfAssuredUnderAge>(rule),
            rule => Assert.IsType<ConfigRegionRuleIfAccountNewerThan>(rule),
            rule => Assert.IsType<ConfigRegionRuleIfAccountOlderThan>(rule));
    }

    [Fact]
    public void KnownRegionRulesRoundTripThroughTheBaseType()
    {
        string[] rules =
        [
            """{"$type":"app.bsky.ageassurance.defs#configRegionRuleDefault","access":"full"}""",
            """{"$type":"app.bsky.ageassurance.defs#configRegionRuleIfDeclaredOverAge","age":18,"access":"safe"}""",
            """{"$type":"app.bsky.ageassurance.defs#configRegionRuleIfDeclaredUnderAge","age":13,"access":"none"}""",
            """{"$type":"app.bsky.ageassurance.defs#configRegionRuleIfAssuredOverAge","age":18,"access":"full"}""",
            """{"$type":"app.bsky.ageassurance.defs#configRegionRuleIfAssuredUnderAge","age":13,"access":"safe"}""",
            """{"$type":"app.bsky.ageassurance.defs#configRegionRuleIfAccountNewerThan","date":"2025-01-01T00:00:00Z","access":"none"}""",
            """{"$type":"app.bsky.ageassurance.defs#configRegionRuleIfAccountOlderThan","date":"2020-01-01T00:00:00Z","access":"full"}"""
        ];

        foreach (string json in rules)
        {
            ConfigRegionRule? rule = JsonSerializer.Deserialize<ConfigRegionRule>(json, BlueskyServer.BlueskyJsonSerializerOptions);
            Assert.NotNull(rule);
            string serialized = JsonSerializer.Serialize(rule, BlueskyServer.BlueskyJsonSerializerOptions);
            using JsonDocument original = JsonDocument.Parse(json);
            using JsonDocument actual = JsonDocument.Parse(serialized);
            Assert.Equal(original.RootElement.GetProperty("$type").GetString(), actual.RootElement.GetProperty("$type").GetString());
            ConfigRegionRule? roundTrip = JsonSerializer.Deserialize<ConfigRegionRule>(serialized, BlueskyServer.BlueskyJsonSerializerOptions);
            Assert.NotNull(roundTrip);
            Assert.Equal(rule.GetType(), roundTrip.GetType());
            Assert.Equal(serialized, JsonSerializer.Serialize(roundTrip, BlueskyServer.BlueskyJsonSerializerOptions));
        }
    }

    [Fact]
    public void UnknownRegionRulePreservesItsDiscriminatorAndFields()
    {
        const string json = """{"$type":"app.bsky.ageassurance.defs#futureRule","access":"futureAccess","nested":{"age":21}}""";
        ConfigRegionRule? rule = JsonSerializer.Deserialize<ConfigRegionRule>(json, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.NotNull(rule);
        Assert.Equal("app.bsky.ageassurance.defs#futureRule", rule.ExtensionData!["$type"].GetString());

        string serialized = JsonSerializer.Serialize(rule, BlueskyServer.BlueskyJsonSerializerOptions);
        using JsonDocument document = JsonDocument.Parse(serialized);
        Assert.Equal("app.bsky.ageassurance.defs#futureRule", document.RootElement.GetProperty("$type").GetString());
        Assert.Equal("futureAccess", document.RootElement.GetProperty("access").GetString());
        Assert.Equal(21, document.RootElement.GetProperty("nested").GetProperty("age").GetInt32());
    }

    [Fact]
    public void StateResultPreservesNestedMetadataAndTimestamps()
    {
        const string json = """
            {"state":{"status":"assured","access":"safe","lastInitiatedAt":"2025-01-02T03:04:05Z"},
             "metadata":{"accountCreatedAt":"2020-01-02T03:04:05Z"}}
            """;
        StateResult? result = JsonSerializer.Deserialize<StateResult>(json, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero), result.State.LastInitiatedAt);
        Assert.Equal(new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero), result.Metadata.AccountCreatedAt);

        string serialized = JsonSerializer.Serialize(result, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.Equal(result, JsonSerializer.Deserialize<StateResult>(serialized, BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Fact]
    public void StateResultRequiresStateAndMetadata()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<StateResult>(
                """{"state":{"status":"assured","access":"safe"}}""",
                BlueskyServer.BlueskyJsonSerializerOptions));
    }
}
