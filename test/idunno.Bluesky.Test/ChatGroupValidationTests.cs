// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class ChatGroupValidationTests
{
    private static readonly Did s_member = new("did:plc:abcdefghijklmnopqrstuvwx");

    [Theory]
    [InlineData("")]
    public async Task CreateGroupThrowsWhenNameIsEmpty(string name)
    {
        using BlueskyAgent agent = new();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => agent.CreateGroup([s_member], name, TestContext.Current.CancellationToken));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public async Task CreateGroupAcceptsANonEmptyName()
    {
        using BlueskyAgent agent = new();

        // Argument validation runs before the authentication check, so an authentication failure
        // means the name was accepted.
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.CreateGroup([s_member], "A group", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetJoinGroupLinkPreviewsThrowsWhenACodeIsNull()
    {
        using BlueskyAgent agent = new();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => agent.GetJoinGroupLinkPreviews(["valid", null!], TestContext.Current.CancellationToken));

        Assert.Equal("codes", exception.ParamName);
    }

    [Fact]
    public async Task GetJoinGroupLinkPreviewsAcceptsPopulatedCodes()
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.GetJoinGroupLinkPreviews(["valid"], TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetJoinGroupLinkPreviewsAcceptsEmptyAndWhitespaceCodes(string code)
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.GetJoinGroupLinkPreviews([code], TestContext.Current.CancellationToken));
    }

    [Fact]
    public void MessagesCopiesRelatedProfiles()
    {
        List<Chat.Actor.ProfileViewBasic> relatedProfiles =
        [
            new Chat.Actor.ProfileViewBasic(s_member, new Handle("example.test"), null, null, null, null, null, null, null, null)
        ];

        Messages messages = new([], null, relatedProfiles);

        relatedProfiles.Clear();

        Assert.Single(messages.RelatedProfiles);
    }

    [Fact]
    public void MessagesRelatedProfilesIsEmptyWhenNotSupplied()
    {
        Messages messages = new();

        Assert.Empty(messages.RelatedProfiles);
    }

    [Fact]
    public void MessagesRejectsNullRelatedProfilesInBothCollectionOverloads()
    {
        IReadOnlyCollection<Chat.Actor.ProfileViewBasic> relatedProfiles = new List<Chat.Actor.ProfileViewBasic> { null! };

        Assert.Throws<ArgumentException>(() => new Messages((IList<MessageViewBase>)[], relatedProfiles: relatedProfiles));
        Assert.Throws<ArgumentException>(() => new Messages((ICollection<MessageViewBase>)[], relatedProfiles: relatedProfiles));
    }
}
