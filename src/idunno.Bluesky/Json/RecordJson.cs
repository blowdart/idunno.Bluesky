// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace idunno.Bluesky.Json;

internal static class RecordJson
{
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
            result = validate ? JsonSerializer.Deserialize(element, typeInfo) : readUnvalidated(element);
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
