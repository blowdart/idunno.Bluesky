// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using idunno.Bluesky.Drafts;

namespace idunno.Bluesky.Json;

internal static class TolerantDraftJson
{
    internal static void CheckType(JsonElement element, string discriminator)
    {
        if (element.ValueKind is not JsonValueKind.Object ||
            (element.TryGetProperty("$type", out JsonElement type) &&
                (type.ValueKind is not JsonValueKind.String || type.GetString() != discriminator)))
        {
            throw new JsonException("The JSON value does not have the expected draft type.");
        }
    }

    private static T Required<T>(JsonElement element, JsonTypeInfo<T> typeInfo, string discriminator) where T : class
    {
        CheckType(element, discriminator);

        return RecordJson.Read(element, typeInfo) ?? throw new JsonException("A draft value must be an object.");
    }

    internal static IReadOnlyList<T>? ReadCollection<T>(JsonElement[]? elements, Func<JsonElement, T> read) =>
        elements is null ? null : Array.AsReadOnly(elements.Select(read).ToArray());

    internal static DraftEmbedImage ReadImage(JsonElement element) =>
        new(Required(element, SourceGenerationContext.Default.DraftImageJsonValue, "app.bsky.draft.defs#draftEmbedImage"));

    internal static DraftEmbedVideo ReadVideo(JsonElement element) =>
        new(Required(element, SourceGenerationContext.Default.DraftVideoJsonValue, "app.bsky.draft.defs#draftEmbedVideo"));

    internal static DraftEmbedCaption ReadCaption(JsonElement element) =>
        new(Required(element, SourceGenerationContext.Default.DraftCaptionJsonValue, "app.bsky.draft.defs#draftEmbedCaption"));

    internal static DraftEmbedLocalRef ReadLocalRef(JsonElement element) =>
        new(Required(element, SourceGenerationContext.Default.DraftLocalRefJsonValue, "app.bsky.draft.defs#draftEmbedLocalRef"));

    internal static DraftEmbedGallery ReadGallery(JsonElement element)
    {
        DraftGalleryJsonValue value = Required(element, SourceGenerationContext.Default.DraftGalleryJsonValue, "app.bsky.draft.defs#draftEmbedGallery");

        return new DraftEmbedGallery(ReadCollection(value.Items, ReadImage) ?? throw new JsonException("Gallery items are required."));
    }

    internal sealed record DraftImageJsonValue(
        [property: JsonRequired] JsonElement LocalRef,
        [property: JsonPropertyName("alt")] string? AltText);

    internal sealed record DraftVideoJsonValue(
        [property: JsonRequired] JsonElement LocalRef,
        [property: JsonPropertyName("alt")] string? AltText,
        JsonElement[]? Captions);

    internal sealed record DraftCaptionJsonValue(
        [property: JsonRequired] string? Lang,
        [property: JsonRequired] string? Content);

    internal sealed record DraftLocalRefJsonValue([property: JsonRequired] string? Path);

    internal sealed record DraftGalleryJsonValue([property: JsonRequired] JsonElement[]? Items);
}
