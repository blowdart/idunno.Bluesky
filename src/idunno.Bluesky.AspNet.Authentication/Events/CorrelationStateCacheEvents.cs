// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// Allow subscribing to events raised by the correlation state cache.
/// </summary>
public class CorrelationStateCacheEvents
{
    /// <summary>
    /// Invoked before correlation state is stored in the correlation state cache.
    /// </summary>
    public Func<CorrelationStateSettingContext, Task> OnStoring { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked after correlation state is retrieved from the correlation state cache.
    /// </summary>
    public Func<CorrelationStateRetrievedContext, Task> OnRetrieved { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked before correlation state is stored in the correlation state cache.
    /// </summary>
    /// <param name="context">The context containing the correlation state to be stored.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task PreStoring(CorrelationStateSettingContext context) => OnStoring(context);

    /// <summary>
    /// Invoked after correlation state is retrieved from the correlation state cache.
    /// </summary>
    /// <param name="context">The context containing the correlation state that was retrieved.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task PostRetrieval(CorrelationStateRetrievedContext context) => OnRetrieved(context);
}
