// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Chat;

internal static class ChatCollection
{
    internal static IReadOnlyList<T> Copy<T>(IEnumerable<T> values) where T : class
    {
        ArgumentNullException.ThrowIfNull(values);
        List<T> copy = [.. values];
        if (copy.Any(value => value is null))
        {
            throw new ArgumentException("Chat collections cannot contain null entries.", nameof(values));
        }

        return copy.AsReadOnly();
    }
}
