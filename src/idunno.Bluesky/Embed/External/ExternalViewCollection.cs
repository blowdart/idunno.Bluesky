// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.Bluesky.Embed.External;

internal static class ExternalViewCollection
{
    internal static T Require<T>(T? value, string propertyName)
        where T : class
    {
        return value ?? throw new JsonException($"The {propertyName} value cannot be null.");
    }

    internal static IReadOnlyCollection<T>? Copy<T>(IReadOnlyCollection<T>? values, string propertyName)
        where T : class
    {
        if (values is null)
        {
            return null;
        }

        if (values.Any(value => value is null))
        {
            throw new JsonException($"The {propertyName} collection cannot contain null entries.");
        }

        return Array.AsReadOnly(values.ToArray());
    }

    internal static IReadOnlyCollection<JsonElement> CopyUnion(IReadOnlyCollection<JsonElement>? values, string propertyName)
    {
        if (values is null)
        {
            throw new JsonException($"The {propertyName} collection cannot be null.");
        }

        JsonElement[] items = new JsonElement[values.Count];
        int index = 0;

        foreach (JsonElement value in values)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"The {propertyName} collection cannot contain non-object entries.");
            }

            items[index++] = value.Clone();
        }

        return Array.AsReadOnly(items);
    }
}
