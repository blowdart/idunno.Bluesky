// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// Context object passed to the <see cref="CorrelationStateCacheEvents.PreStoring(CorrelationStateSettingContext)"/> method.
/// </summary>
/// <param name="state">The serialized correlation state to be stored.</param>
/// <remarks>
/// <para>Creates a new instance of <see cref="CorrelationStateSettingContext"/>.</para>
/// </remarks>
public class CorrelationStateSettingContext(string state)
{
    /// <summary>
    /// Gets the serialized correlation state to be stored.
    /// </summary>
    public string State { get; private set; } = state;

    /// <summary>
    /// Replaces the serialized correlation state to be stored.
    /// </summary>
    /// <param name="state">The new serialized correlation state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null" />.</exception>
    public void ReplaceState(string state)
    {
        ArgumentNullException.ThrowIfNull(state);

        State = state;
    }
}
