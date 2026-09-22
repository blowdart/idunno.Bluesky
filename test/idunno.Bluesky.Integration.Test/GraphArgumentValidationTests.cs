// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Integration.Test;

/// <summary>
/// Covers the argument validation of the graph APIs.
/// </summary>
/// <remarks>
/// <para>Several of these guards validated after checking authentication, or reported a message which was written as
/// a plain string rather than an interpolated one, so the placeholder reached the caller verbatim.</para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class GraphArgumentValidationTests
{
    private static readonly Did s_did = "did:plc:test";

    private static readonly AtUri s_list = new("at://did:plc:test/app.bsky.graph.list/1");

    private static readonly AtUri s_post = new("at://did:plc:test/app.bsky.feed.post/1");

    private static readonly AtUri s_optOut = new("at://did:plc:test/app.bsky.graph.referencelistoptout/1");

    private static readonly Cid s_cid = new("bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy");

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    /// <summary>
    /// The lexicon constrains limit to 1..100. Every other graph endpoint validated it, this one sent it on and let
    /// the service answer with an opaque 400.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task GetActorStarterPacksRejectsALimitOutsideTheLexiconRange(int limit)
    {
        using HttpClient httpClient = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await BlueskyServer.GetActorStarterPacks(
            actor: s_did,
            limit: limit,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A list item record must point at a list. Without this guard an entry could be created against any collection.
    /// </summary>
    [Fact]
    public async Task AddToListRejectsAUriWhichIsNotAList()
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await agent.AddToList(
            s_post,
            s_did,
            cancellationToken: TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await agent.AddToList(
            s_post,
            new Handle("test.invalid"),
            cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Argument validation runs before the authentication check, so an unauthenticated caller passing a bad argument
    /// is told which argument is wrong rather than being told to authenticate first.
    /// </summary>
    [Fact]
    public async Task DeleteReferenceListOptOutValidatesItsArgumentsBeforeCheckingAuthentication()
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await agent.DeleteReferenceListOptOut(
            (AtUri)null!,
            TestContext.Current.CancellationToken));

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () => await agent.DeleteReferenceListOptOut(
            s_list,
            TestContext.Current.CancellationToken));

        Assert.Equal("uri", exception.ParamName);
    }

    [Fact]
    public async Task DeleteReferenceListOptOutAcceptsAUriInTheCorrectCollection()
    {
        using BlueskyAgent agent = new();

        // The collection is correct, so validation passes and the authentication check is what rejects the call.
        await Assert.ThrowsAsync<AuthenticationRequiredException>(async () => await agent.DeleteReferenceListOptOut(
            s_optOut,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The message was written without its interpolation prefix, so it read "does not point to an
    /// {RecordCollections.Post} record" and named a type which does not exist.
    /// </summary>
    [Fact]
    public async Task RepostReportsTheCollectionItExpectedInItsMessage()
    {
        using BlueskyAgent agent = new();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () => await agent.Repost(
            new StrongReference(s_list, s_cid),
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(CollectionNsid.Post.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordCollections", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("{", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteRepostReportsTheCollectionsItExpectedInItsMessage()
    {
        using BlueskyAgent agent = new();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () => await agent.DeleteRepost(
            s_list,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(CollectionNsid.Post.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(CollectionNsid.Repost.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("{", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <see cref="AtUri.Repo"/> is not nullable, so the guards which tested it for null could never fire. The
    /// remaining guards report the component which is actually missing, and now name the uri they were given.
    /// </summary>
    [Fact]
    public async Task GetRecordReportsTheMissingUriComponentInItsMessage()
    {
        using BlueskyAgent agent = new();

        AtUri repoOnly = new("at://did:plc:test");

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () => await agent.GetRecord<AtProtoRecord>(
            repoOnly,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(repoOnly.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("collection", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("{uri}", exception.Message, StringComparison.Ordinal);
    }
}
