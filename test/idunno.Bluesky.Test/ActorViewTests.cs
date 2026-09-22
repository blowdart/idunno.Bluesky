// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class ActorViewTests
{
    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");

    private static readonly Handle s_handle = new("test.invalid");

    private static Label CreateSelfLabel(string value) =>
        new(null, s_did, $"at://{s_did}/app.bsky.actor.profile/self", null, value, false, DateTimeOffset.UtcNow, []);

    private static ProfileViewBasic CreateProfileViewBasic(IReadOnlyCollection<Label>? labels) =>
        new(s_did, s_handle, null, null, null, null, null, null, labels, null, null, null);

    [Fact]
    public void ProfileViewBasicSelfLabelsFollowTheLabelsItCurrentlyHolds()
    {
        ProfileViewBasic profile = CreateProfileViewBasic([CreateSelfLabel("!no-unauthenticated")]);

        Assert.Single(profile.SelfLabels);

        ProfileViewBasic without = profile with { Labels = [] };

        Assert.Empty(without.SelfLabels);
    }

    [Fact]
    public void ProfileViewBasicCopiesTheLabelsItIsGiven()
    {
        List<Label> labels = [CreateSelfLabel("!no-unauthenticated")];

        ProfileViewBasic profile = CreateProfileViewBasic(labels);

        labels.Clear();

        Assert.Single(profile.Labels);
    }

    [Fact]
    public void VerificationStateProjectionsFollowTheValuesItCurrentlyHolds()
    {
        VerificationState state = new([], "valid", "none");

        Assert.Equal(VerificationStatus.Valid, state.VerifiedStatus);
        Assert.Equal(VerificationStatus.None, state.TrustedVerifierStatus);

        VerificationState changed = state with { VerifiedStatusString = "invalid" };

        Assert.Equal(VerificationStatus.Invalid, changed.VerifiedStatus);
    }

    [Fact]
    public void KnownFollowersCopiesTheFollowersItIsGiven()
    {
        List<ProfileViewBasic> followers = [CreateProfileViewBasic(null)];

        KnownFollowers knownFollowers = new(1, followers);

        followers.Clear();

        Assert.Single(knownFollowers.Followers);
    }

    [Fact]
    public void SavedFeedsPreferenceCopiesTheCollectionsItIsGiven()
    {
        List<AtUri> saved = [new AtUri($"at://{s_did}/app.bsky.feed.generator/feed")];
        List<AtUri> pinned = [new AtUri($"at://{s_did}/app.bsky.feed.generator/feed")];

        SavedFeedsPreference preference = new(saved, pinned, null);

        saved.Clear();
        pinned.Clear();

        Assert.Single(preference.Saved);
        Assert.Single(preference.Pinned);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StatusRejectsADurationBelowTheLexiconMinimum(int durationMinutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Status("app.bsky.actor.status#live", null, durationMinutes, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StatusAcceptsTheLexiconMinimumDuration()
    {
        Status status = new("app.bsky.actor.status#live", null, 1, DateTimeOffset.UtcNow);

        Assert.Equal(1, status.DurationMinutes);
    }
}
