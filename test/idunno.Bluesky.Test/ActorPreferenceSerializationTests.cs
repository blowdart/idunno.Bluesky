// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.Bluesky.Actor;
using idunno.Bluesky.Json;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class ActorPreferenceSerializationTests
{
    private static readonly JsonSerializerOptions s_options = BlueskyServer.BlueskyJsonSerializerOptions;

    [Fact]
    public void UnrecognizedPreferenceKeepsItsTypeDiscriminatorWhenRoundTripped()
    {
        const string json = """[{"$type":"app.bsky.actor.defs#someFuturePref","someSetting":"keepMe","count":7}]""";

        IList<Preference>? preferences = JsonSerializer.Deserialize<IList<Preference>>(json, s_options);

        Assert.NotNull(preferences);
        Preference preference = Assert.Single(preferences);
        Assert.Equal(typeof(Preference), preference.GetType());

        string roundTripped = JsonSerializer.Serialize(preferences, s_options);

        Assert.Contains(@"""$type"":""app.bsky.actor.defs#someFuturePref""", roundTripped, StringComparison.Ordinal);
        Assert.Contains(@"""someSetting"":""keepMe""", roundTripped, StringComparison.Ordinal);
        Assert.Contains(@"""count"":7", roundTripped, StringComparison.Ordinal);
    }

    [Fact]
    public void UnrecognizedPreferenceIsNotMangledWhenTheWholePreferenceSetIsWrittenBack()
    {
        const string json = """
            [
              {"$type":"app.bsky.actor.defs#adultContentPref","enabled":true},
              {"$type":"app.bsky.actor.defs#someFuturePref","someSetting":"keepMe"}
            ]
            """;

        IList<Preference> preferences = JsonSerializer.Deserialize<IList<Preference>>(json, s_options)!;

        string roundTripped = JsonSerializer.Serialize(new Preferences(preferences, enableBlueskyModerationLabeler: false), s_options);

        Assert.Equal(
            """[{"$type":"app.bsky.actor.defs#adultContentPref","enabled":true},{"$type":"app.bsky.actor.defs#someFuturePref","someSetting":"keepMe"}]""",
            roundTripped);
    }

    [Theory]
    [InlineData("app.bsky.actor.defs#personalDetailsPref", typeof(PersonalDetailsPreference))]
    [InlineData("app.bsky.actor.defs#declaredAgePref", typeof(DeclaredAgePreference))]
    [InlineData("app.bsky.actor.defs#threadViewPref", typeof(ThreadViewPreference))]
    [InlineData("app.bsky.actor.defs#mutedWordsPref", typeof(MutedWordPreferences))]
    [InlineData("app.bsky.actor.defs#hiddenPostsPref", typeof(HiddenPostsPreferences))]
    [InlineData("app.bsky.actor.defs#bskyAppStatePref", typeof(BlueskyAppStatePreference))]
    [InlineData("app.bsky.actor.defs#postInteractionSettingsPref", typeof(PostInteractionSettingsPreferences))]
    [InlineData("app.bsky.actor.defs#verificationPrefs", typeof(VerificationPreferences))]
    [InlineData("app.bsky.actor.defs#liveEventPreferences", typeof(LiveEventPreferences))]
    public void RecognizedPreferenceDeserializesToItsTypeAndKeepsItsDiscriminator(string discriminator, Type expectedType)
    {
        Preference? preference = JsonSerializer.Deserialize<Preference>($$"""{"$type":"{{discriminator}}"}""", s_options);

        Assert.NotNull(preference);
        Assert.Equal(expectedType, preference.GetType());

        string roundTripped = JsonSerializer.Serialize(preference, s_options);

        Assert.Contains($@"""$type"":""{discriminator}""", roundTripped, StringComparison.Ordinal);
    }

    [Fact]
    public void RecognizedPreferenceDoesNotWriteItsTypeDiscriminatorTwice()
    {
        Preference? preference = JsonSerializer.Deserialize<Preference>(
            """{"$type":"app.bsky.actor.defs#adultContentPref","enabled":true}""", s_options);

        string roundTripped = JsonSerializer.Serialize(preference, s_options);

        Assert.Equal("""{"$type":"app.bsky.actor.defs#adultContentPref","enabled":true}""", roundTripped);
        Assert.Null(preference!.ExtensionData);
    }

    [Fact]
    public void PreferenceWithNoTypeDiscriminatorIsReadAsABasePreference()
    {
        Preference? preference = JsonSerializer.Deserialize<Preference>("""{"someSetting":"value"}""", s_options);

        Assert.NotNull(preference);
        Assert.Equal(typeof(Preference), preference.GetType());
        Assert.NotNull(preference.ExtensionData);
        Assert.True(preference.ExtensionData.ContainsKey("someSetting"));
    }

    [Fact]
    public void EveryPreferenceTypeIsMappedToATypeDiscriminator()
    {
        IEnumerable<Type> derivedPreferenceTypes = typeof(Preference).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(Preference)));

        IReadOnlyCollection<Type> mappedTypes = [.. PreferenceConverter.DiscriminatorToType.Values];

        List<Type> unmapped = [.. derivedPreferenceTypes.Where(type => !mappedTypes.Contains(type))];

        Assert.True(
            unmapped.Count == 0,
            $"These preference types are not mapped to a type discriminator and would lose their data when written: {string.Join(", ", unmapped.Select(type => type.Name))}");
    }
}
