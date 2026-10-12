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
    public void StateResultRequiresStateAndMetadata()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<StateResult>(
                """{"state":{"status":"assured","access":"safe"}}""",
                BlueskyServer.BlueskyJsonSerializerOptions));
    }
}
