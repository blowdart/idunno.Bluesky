// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Internal;

namespace idunno.AtProto;

/// <summary>
/// Adapts a <see cref="TimeProvider"/> to the <see cref="ISystemClock"/> a <see cref="Microsoft.Extensions.Caching.Memory.MemoryCache"/> expires entries by.
/// </summary>
/// <param name="timeProvider">The time provider to read the current time from.</param>
internal sealed class TimeProviderSystemClock(TimeProvider timeProvider) : ISystemClock
{
    /// <summary>
    /// Gets the current time from the time provider.
    /// </summary>
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
