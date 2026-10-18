// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky.Json;

internal static class RecordJson
{
    internal static JsonSerializerOptions Options { get; } = BlueskyJsonSerializerOptions.Create(Configure);

    internal static void Configure(JsonSerializerOptions options)
    {
        options.TypeInfoResolver = options.TypeInfoResolver!.WithAddedModifier(typeInfo =>
        {
            if (typeInfo.Type == typeof(Blob))
            {
                foreach (JsonPropertyInfo property in typeInfo.Properties.Where(property => property.Name is "ref" or "mimeType" or "size"))
                {
                    property.IsRequired = true;
                }

                typeInfo.OnDeserialized = value =>
                {
                    Blob blob = (Blob)value;
                    if (blob.Reference?.Link is null || blob.MimeType is null)
                    {
                        throw new JsonException("A blob requires a reference, MIME type and size.");
                    }
                };
            }
            else if (typeInfo.Type == typeof(ReplyReference))
            {
                typeInfo.OnDeserialized = value =>
                {
                    if (((ReplyReference)value).MessageId is null)
                    {
                        throw new JsonException("A reply message ID is required.");
                    }
                };
            }
        });
    }

    internal static void CheckDiscriminator(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Object &&
            element.TryGetProperty("$type", out JsonElement discriminator) &&
            discriminator.ValueKind is not JsonValueKind.String)
        {
            throw new JsonException("A type discriminator must be a string.");
        }
    }

    internal static T? Read<T>(JsonElement element, JsonTypeInfo<T> typeInfo)
    {
        CheckDiscriminator(element);

        return JsonSerializer.Deserialize(element, (JsonTypeInfo<T>)Options.GetTypeInfo(typeInfo.Type));
    }

    internal static T Parse<T>(JsonElement element, bool validate, JsonTypeInfo<T> typeInfo, Func<JsonElement, T?> readUnvalidated)
        where T : class, IRecordValidatable
    {
        if (TryParse(element, validate, typeInfo, readUnvalidated, out T? result))
        {
            return result;
        }

        throw new JsonException($"The JSON value does not contain a {typeof(T).Name} that passes the selected validation mode.");
    }

    internal static bool TryParse<T>(
        JsonElement element,
        bool validate,
        JsonTypeInfo<T> typeInfo,
        Func<JsonElement, T?> readUnvalidated,
        [MaybeNullWhen(false)] out T result)
        where T : class, IRecordValidatable
    {
        result = null;
        if (element.ValueKind is not JsonValueKind.Object)
        {
            return false;
        }

        try
        {
            CheckDiscriminator(element);
            result = validate ? Read(element, typeInfo) : readUnvalidated(element);
            if (result is null || (validate && result.ValidateRecord().Any()))
            {
                result = null;
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            result = null;
            return false;
        }
        catch (ArgumentException)
        {
            result = null;
            return false;
        }
    }

    internal static IDictionary<string, JsonElement> CopyExtensionData(IDictionary<string, JsonElement> data) =>
        data.Where(property => property.Key != "$type")
            .ToDictionary(property => property.Key, property => property.Value.Clone());
}
