// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class DeleteLikeTests
{
    private const string PostUri = "at://did:plc:ar7c4by46qjdydhdevvrndac/app.bsky.feed.post/3kk2bwvyjq52s";
    private const string LikeUri = "at://did:plc:ar7c4by46qjdydhdevvrndac/app.bsky.feed.like/3kk2bwvyjq52s";
    private const string FollowUri = "at://did:plc:ar7c4by46qjdydhdevvrndac/app.bsky.graph.follow/3kk2bwvyjq52s";

    [Theory]
    [InlineData(PostUri)]
    [InlineData(LikeUri)]
    public async Task DeleteLikeAcceptsBothAPostUriAndALikeUri(string uri)
    {
        // The collection is validated before the authentication check, so reaching the authentication
        // check proves the uri was let through.
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.DeleteLike(new AtUri(uri), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteLikeRejectsAUriWhichIsNeitherAPostNorALike()
    {
        using BlueskyAgent agent = new();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => agent.DeleteLike(new AtUri(FollowUri), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("uri", exception.ParamName);
    }

    [Fact]
    public async Task DeleteLikeFromAStrongReferenceToAPostIsAccepted()
    {
        // A StrongReference to a post carries a post uri, so this overload must accept the post collection.
        StrongReference strongReference = new(
            new AtUri(PostUri),
            new Cid("bafyreid27zk7lbis4zw5fz4podbvbs4fc5ivwji3dmrwa6zggnj4bnd57u"));

        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.DeleteLike(strongReference, cancellationToken: TestContext.Current.CancellationToken));
    }
}
