// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Bluesky.Actor
{
    /// <summary>
    /// Data associated with an actor profile.
    /// </summary>
    /// <param name="Lists">The number of lists associated with an actor.</param>
    /// <param name="Feedgens">The number of feed generators associated with an actor.</param>
    /// <param name="Labeler">A flag indicating whether the actor is a labeler.</param>
    public record Associated(int Lists, int Feedgens, bool Labeler);
}
