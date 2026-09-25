// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Notifications;

/// <summary>
/// Represents the input into or response from the Notifications PutActivitySubscription API.
/// </summary>
/// <param name="Subject">The <see cref="Did"/> the subscription applies to.</param>
/// <param name="ActivitySubscription">
///   An <see cref="Notifications.ActivitySubscription"/> representing the subscription settings, if any.
/// </param>
/// <remarks>
///   <para>
///     The lexicon declares <paramref name="ActivitySubscription"/> as required on input but optional on output,
///     so it is <see langword="null"/> when a server omits it from a response.
///   </para>
/// </remarks>
public sealed record SubjectActivitySubscription(Did Subject, ActivitySubscription? ActivitySubscription)
{
}