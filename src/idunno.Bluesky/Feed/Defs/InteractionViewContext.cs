// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// The views in which an <see cref="Interaction"/> can occur.
/// </summary>
[JsonConverter(typeof(InteractionViewContextConverter))]
public enum InteractionViewContext
{
    /// <summary>
    /// The interaction occurred while viewing a feed.
    /// </summary>
    Feed,

    /// <summary>
    /// The interaction occurred while viewing a thread.
    /// </summary>
    Thread,

    /// <summary>
    /// The view context is not one this library recognizes. It cannot be sent to a service.
    /// </summary>
    Unknown
}
