// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// Context object passed to the <see cref="CorrelationStateCacheEvents.PostRetrieval(CorrelationStateRetrievedContext)"/> method.
/// </summary>
/// <param name="state">The serialized correlation state that has been retrieved.</param>
/// <remarks>
/// <para>Creates a new instance of <see cref="CorrelationStateRetrievedContext"/>.</para>
/// </remarks>
public class CorrelationStateRetrievedContext(string state)
{
    /// <summary>
    /// Gets the serialized correlation state that has been retrieved from the correlation state cache.
    /// </summary>
    public string State { get; private set; } = state;

    /// <summary>
    /// Replaces the serialized correlation state that has been retrieved from the correlation state cache.
    /// </summary>
    /// <param name="state">The new serialized correlation state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null" />.</exception>
    public void ReplaceState(string state)
    {
        ArgumentNullException.ThrowIfNull(state);

        State = state;
    }
}
