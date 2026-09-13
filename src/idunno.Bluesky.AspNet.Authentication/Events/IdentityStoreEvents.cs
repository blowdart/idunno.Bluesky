// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// Allow subscribing to events raised by the identity store.
/// </summary>
public class IdentityStoreEvents
{
    /// <summary>
    /// Invoked before an identity is stored in the identity store. The string parameter is the serialized identity to be stored.
    /// </summary>
    public Func<IdentityStoreSettingContext, Task> OnStoring { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked after an identity is retrieved from the identity store. The string parameter is the serialized identity that was retrieved.
    /// </summary>
    public Func<IdentityStoreRetrievedContext, Task> OnRetrieved { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked before an identity is stored in the identity store
    /// </summary>
    /// <param name="context">The context containing the identity to be stored.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task PreStoring(IdentityStoreSettingContext context) => OnStoring(context);

    /// <summary>
    /// Invoked after an identity is retrieved from the identity store
    /// </summary>
    /// <param name="context">The context containing the identity that was retrieved.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task PostRetrieval(IdentityStoreRetrievedContext context) => OnRetrieved(context);
}
