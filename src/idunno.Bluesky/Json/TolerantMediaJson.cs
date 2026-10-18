// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Json;

internal static class TolerantMediaJson
{
    internal static JsonTypeInfo<EmbeddedBase> EmbeddedBaseTypeInfo { get; } = CreateTypeInfo();

    private static JsonTypeInfo<EmbeddedBase> CreateTypeInfo()
    {
        JsonSerializerOptions options = new(SourceGenerationContext.Default.Options);
        options.Converters.Add(new AspectRatioConverter());
        options.Converters.Add(new CaptionConverter());
        options.Converters.Add(new ExternalPropertiesConverter());
        options.TypeInfoResolver = SourceGenerationContext.Default.WithAddedModifier(typeInfo =>
        {
            if (typeInfo.Type == typeof(Embed.External.Properties))
            {
                // The opt-in converter reads the entire card, including its optional discriminator.
                typeInfo.PolymorphismOptions = null;
            }
        });
        options.MakeReadOnly();

        return (JsonTypeInfo<EmbeddedBase>)options.GetTypeInfo(typeof(EmbeddedBase));
    }

    private sealed class AspectRatioConverter : JsonConverter<AspectRatio>
    {
        public override AspectRatio Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            AspectRatioJsonValue value = JsonSerializer.Deserialize(ref reader, SourceGenerationContext.Default.AspectRatioJsonValue)
                ?? throw new JsonException("An aspect ratio must be an object.");

            return new AspectRatio(value.Width, value.Height, validate: false);
        }

        public override void Write(Utf8JsonWriter writer, AspectRatio value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, SourceGenerationContext.Default.AspectRatio);
        }
    }

    private sealed class CaptionConverter : JsonConverter<Caption>
    {
        public override Caption Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            CaptionJsonValue value = JsonSerializer.Deserialize(ref reader, SourceGenerationContext.Default.CaptionJsonValue)
                ?? throw new JsonException("A caption must be an object.");

            if (value.Lang is null || value.File is null)
            {
                throw new JsonException("A caption must contain a language and file.");
            }

            return new Caption(value.Lang, value.File, validate: false);
        }

        public override void Write(Utf8JsonWriter writer, Caption value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, SourceGenerationContext.Default.Caption);
        }
    }

    private sealed class ExternalPropertiesConverter : JsonConverter<Embed.External.Properties>
    {
        public override Embed.External.Properties Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            ExternalPropertiesJsonValue value = JsonSerializer.Deserialize(ref reader, SourceGenerationContext.Default.ExternalPropertiesJsonValue)
                ?? throw new JsonException("External card properties must be an object.");

            if (value.Uri is null || value.Title is null || value.Description is null)
            {
                throw new JsonException("An external card must contain a URI, title and description.");
            }

            return new Embed.External.Properties(value.Uri, value.Title, value.Description, value.Thumbnail, value.AssociatedRefs, validate: false);
        }

        public override void Write(Utf8JsonWriter writer, Embed.External.Properties value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, SourceGenerationContext.Default.AppBskyEmbedExternalExternal);
        }
    }

    internal sealed record AspectRatioJsonValue(
        [property: JsonRequired] int Width,
        [property: JsonRequired] int Height);

    internal sealed record CaptionJsonValue(
        [property: JsonRequired] string? Lang,
        [property: JsonRequired] Blob? File);

    internal sealed record ExternalPropertiesJsonValue(
        [property: JsonRequired] string? Uri,
        [property: JsonRequired] string? Title,
        [property: JsonRequired] string? Description,
        [property: JsonPropertyName("thumb")] Blob? Thumbnail,
        IReadOnlyCollection<StrongReference>? AssociatedRefs);
}
