// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Json;

/// <summary>
/// Converts a <see cref="Preference"/> to or from JSON, preserving the type discriminator of preferences
/// whose type is not known to this library.
/// </summary>
/// <remarks>
/// <para>
/// The <c>app.bsky.actor.defs#preferences</c> lexicon is an open union and Bluesky adds new members to it over time.
/// The default <see cref="System.Text.Json"/> polymorphic reader consumes the <c>$type</c> property before falling
/// back to the nearest ancestor, so an unrecognized preference would deserialize to a plain <see cref="Preference"/>
/// whose discriminator has been discarded. Writing that preference back, as
/// <see cref="BlueskyAgent.PutPreferences(Preferences)"/> does when it replaces the actor's entire preference set,
/// would then emit an object with no <c>$type</c> and silently corrupt or drop the preference.
/// </para>
/// <para>
/// This converter keeps every property of an unrecognized preference, including its discriminator, in
/// <see cref="Preference.ExtensionData"/> so that a read/modify/write round trip leaves preferences this library
/// does not understand untouched.
/// </para>
/// </remarks>
internal sealed class PreferenceConverter : JsonConverter<Preference>
{
    private const string TypeDiscriminatorPropertyName = "$type";

    private static readonly Dictionary<string, Type> s_discriminatorToType = BuildDiscriminatorToTypeMap();

    private static readonly Dictionary<Type, string> s_typeToDiscriminator =
        s_discriminatorToType.ToDictionary(entry => entry.Value, entry => entry.Key);

    /// <summary>
    /// Gets a value indicating whether the specified <paramref name="typeToConvert"/> can be converted.
    /// </summary>
    /// <param name="typeToConvert">The type to check.</param>
    /// <returns><see langword="true"/> if <paramref name="typeToConvert"/> is <see cref="Preference"/>, otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Only the <see cref="Preference"/> base type is handled. Derived preferences are serialized by their own
    /// contract, which is what stops the calls this converter makes to <see cref="JsonSerializer"/> re-entering it.
    /// </remarks>
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Preference);

    /// <summary>
    /// Reads and converts JSON to a <see cref="Preference"/>.
    /// </summary>
    /// <param name="reader">The reader to read from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> to use when reading.</param>
    /// <returns>The <see cref="Preference"/> that was read.</returns>
    /// <exception cref="JsonException">Thrown when the JSON being read is not an object.</exception>
    public override Preference? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement element = document.RootElement;

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Expected an object when reading a {nameof(Preference)} but found {element.ValueKind}.");
        }

        if (element.TryGetProperty(TypeDiscriminatorPropertyName, out JsonElement discriminatorElement) &&
            discriminatorElement.ValueKind == JsonValueKind.String &&
            discriminatorElement.GetString() is string discriminator &&
            s_discriminatorToType.TryGetValue(discriminator, out Type? derivedType))
        {
            return ReadKnownPreference(element, derivedType, options);
        }

        Dictionary<string, JsonElement> extensionData = [];

        foreach (JsonProperty property in element.EnumerateObject())
        {
            extensionData[property.Name] = property.Value.Clone();
        }

        return new Preference
        {
            ExtensionData = extensionData
        };
    }

    /// <summary>
    /// Writes the specified <paramref name="value"/> as JSON.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The <see cref="Preference"/> to write.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> to use when writing.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/>, <paramref name="value"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public override void Write(Utf8JsonWriter writer, Preference value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        if (!s_typeToDiscriminator.TryGetValue(value.GetType(), out string? discriminator))
        {
            WriteExtensionDataOnly(writer, value);
            return;
        }

        byte[] serializedPreference = JsonSerializer.SerializeToUtf8Bytes(value, options.GetTypeInfo(value.GetType()));

        using JsonDocument document = JsonDocument.Parse(serializedPreference);

        writer.WriteStartObject();
        writer.WriteString(TypeDiscriminatorPropertyName, discriminator);

        foreach (JsonProperty property in document.RootElement.EnumerateObject().Where(property => !property.NameEquals(TypeDiscriminatorPropertyName)))
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static Preference? ReadKnownPreference(JsonElement element, Type derivedType, JsonSerializerOptions options)
    {
        // The discriminator is removed before the preference is handed to its own contract, otherwise it would be
        // captured as extension data and then written out a second time alongside the discriminator Write() emits.
        ArrayBufferWriter<byte> buffer = new();

        using (Utf8JsonWriter bufferWriter = new(buffer))
        {
            bufferWriter.WriteStartObject();

            foreach (JsonProperty property in element.EnumerateObject().Where(property => !property.NameEquals(TypeDiscriminatorPropertyName)))
            {
                property.WriteTo(bufferWriter);
            }

            bufferWriter.WriteEndObject();
        }

        Utf8JsonReader derivedReader = new(buffer.WrittenSpan);

        return (Preference?)JsonSerializer.Deserialize(ref derivedReader, options.GetTypeInfo(derivedType));
    }

    private static void WriteExtensionDataOnly(Utf8JsonWriter writer, Preference value)
    {
        writer.WriteStartObject();

        if (value.ExtensionData is not null)
        {
            foreach (KeyValuePair<string, JsonElement> property in value.ExtensionData)
            {
                writer.WritePropertyName(property.Key);
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static Dictionary<string, Type> BuildDiscriminatorToTypeMap() => new()
    {
        [PreferenceTypeDiscriminators.AdultContent] = typeof(AdultContentPreference),
        [PreferenceTypeDiscriminators.BlueskyAppState] = typeof(BlueskyAppStatePreference),
        [PreferenceTypeDiscriminators.ContentLabel] = typeof(ContentLabelPreference),
        [PreferenceTypeDiscriminators.DeclaredAge] = typeof(DeclaredAgePreference),
        [PreferenceTypeDiscriminators.FeedView] = typeof(FeedViewPreference),
        [PreferenceTypeDiscriminators.HiddenPosts] = typeof(HiddenPostsPreferences),
        [PreferenceTypeDiscriminators.Interests] = typeof(InterestsPreference),
        [PreferenceTypeDiscriminators.Labelers] = typeof(LabelersPreference),
        [PreferenceTypeDiscriminators.LiveEvents] = typeof(LiveEventPreferences),
        [PreferenceTypeDiscriminators.MutedWords] = typeof(MutedWordPreferences),
        [PreferenceTypeDiscriminators.PersonalDetails] = typeof(PersonalDetailsPreference),
        [PreferenceTypeDiscriminators.PostInteraction] = typeof(PostInteractionSettingsPreferences),
        [PreferenceTypeDiscriminators.SavedFeeds] = typeof(SavedFeedsPreference),
        [PreferenceTypeDiscriminators.SavedFeedsV2] = typeof(SavedFeedPreferencesV2),
        [PreferenceTypeDiscriminators.ThreadView] = typeof(ThreadViewPreference),
        [PreferenceTypeDiscriminators.Verification] = typeof(VerificationPreferences),
    };

    /// <summary>
    /// Gets the preference types this converter can map to a type discriminator, keyed by that discriminator.
    /// </summary>
    /// <remarks>
    /// Exposed so that tests can assert every type deriving from <see cref="Preference"/> is mapped. A type that is
    /// missing from the map is treated as an unrecognized preference and loses its properties when written.
    /// </remarks>
    internal static IReadOnlyDictionary<string, Type> DiscriminatorToType => s_discriminatorToType;
}
