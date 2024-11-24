// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Actor
{
    public record ProfileAssociated
    {
        public ProfileAssociated(int lists, int feedGens, int starterPacks, bool labeler, ProfileAssociatedChat? chat)
        {
            Lists = lists;
            FeedGens = feedGens;
            StarterPacks = starterPacks;
            Labeler = labeler;
            Chat = chat;
        }

        public int Lists { get; init; }

        public int FeedGens { get; init; }

        public int StarterPacks { get; init; }

        public bool Labeler { get; init; }

        public ProfileAssociatedChat? Chat { get; init; }
    }
}
