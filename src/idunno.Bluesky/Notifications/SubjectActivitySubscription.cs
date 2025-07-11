// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Notifications
{
    /// <summary>
    /// Represents the input into or response from the Notifications PutActivitySubscription API.
    /// </summary>
    /// <param name="Subject">The <see cref="Did"/> the subscription applies to.</param>
    /// <param name="ActivitySubscription">An <see cref="ActivitySubscription"/> representing the subscription settings.</param>
    public sealed record SubjectActivitySubscription(Did Subject, ActivitySubscription ActivitySubscription)
    {
    }
}
