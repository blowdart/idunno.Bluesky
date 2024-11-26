// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Metadata associated with an actor's profile.
    /// </summary>
    public record ProfileAssociated
    {
        /// <summary>
        /// Creates a new instance of <see cref="ProfileAssociated"/>.
        /// </summary>
        /// <param name="lists">The number of lists the actor currently has.</param>
        /// <param name="feedGens">The number of feed gens the actor currently has.</param>
        /// <param name="starterPacks">The number of starter packs the actor currently has.</param>
        /// <param name="labeler">A flag indicating whether the actor is a labeler.</param>
        /// <param name="chat">The chat configuration for the actor.</param>
        public ProfileAssociated(int lists, int feedGens, int starterPacks, bool labeler, ProfileAssociatedChat? chat)
        {
            Lists = lists;
            FeedGens = feedGens;
            StarterPacks = starterPacks;
            Labeler = labeler;
            Chat = chat;
        }

        /// <summary>
        /// Gets the number of lists the actor currently has.
        /// </summary>
        public int Lists { get; init; }

        /// <summary>
        /// Gets the number of feed gens the actor currently has.
        /// </summary>

        public int FeedGens { get; init; }

        /// <summary>
        /// Gets the number of starter packs the actor currently has.
        /// </summary>

        public int StarterPacks { get; init; }

        /// <summary>
        /// Gets a flag indicating whether the actor is a labeler.
        /// </summary>
        public bool Labeler { get; init; }

        /// <summary>
        /// Gets the chat configuration for the actor.
        /// </summary>
        public ProfileAssociatedChat? Chat { get; init; }
    }
}
