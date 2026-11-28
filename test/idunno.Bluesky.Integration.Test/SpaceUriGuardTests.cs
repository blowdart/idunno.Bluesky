// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable BSKYSpaces

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Graph;

using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class SpaceUriGuardTests
{
    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");

    // The space is owned by the authenticated user, so authority checks alone would not stop the call.
    private static readonly SpaceAtUri s_spaceRecord = SpaceAtUri.Parse(
        "at:space/did:plc:hfgp6pj3akhqxntgqwramlbg/com.example.group/default/did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x",
        provider: null);

    private static readonly Cid s_cid = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    public static TheoryData<string> GuardedOperations =>
    [
        nameof(BlueskyAgent.DeleteRecord),
        "DeleteRecordStrongReference",
        nameof(BlueskyAgent.GetRawRecord),
        nameof(BlueskyAgent.GetBlueskyRecord),
        nameof(BlueskyAgent.DeletePost),
        nameof(BlueskyAgent.DeleteLike),
        nameof(BlueskyAgent.DeleteRepost),
        nameof(BlueskyAgent.DeleteBlock),
        nameof(BlueskyAgent.DeleteFollow),
        nameof(BlueskyAgent.AddThreadGate),
        nameof(BlueskyAgent.DeleteThreadGate),
        nameof(BlueskyAgent.GetPostGate),
        nameof(BlueskyAgent.CreateReferenceListOptOut),
        nameof(BlueskyAgent.UpdateList),
    ];

    [Theory]
    [MemberData(nameof(GuardedOperations))]
    public async Task SpaceRecordUrisAreRejectedBeforeAnyRequestIsSent(string operation)
    {
        bool serverWasCalled = false;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            context =>
            {
                serverWasCalled = true;
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Task.CompletedTask;
            });

        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer))
        {
            Credentials = CreateCredentials(),
            Service = TestServerBuilder.DefaultUri
        };

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Func<Task> act = operation switch
        {
            nameof(BlueskyAgent.DeleteRecord) => () => agent.DeleteRecord(s_spaceRecord, cancellationToken: cancellationToken),
            "DeleteRecordStrongReference" => () => agent.DeleteRecord(new StrongReference(s_spaceRecord, s_cid), cancellationToken: cancellationToken),
            nameof(BlueskyAgent.GetRawRecord) => () => agent.GetRawRecord(s_spaceRecord, cancellationToken),
            nameof(BlueskyAgent.GetBlueskyRecord) => () => agent.GetBlueskyRecord<Post>(s_spaceRecord, cancellationToken: cancellationToken),
            nameof(BlueskyAgent.DeletePost) => () => agent.DeletePost(s_spaceRecord, cancellationToken),
            nameof(BlueskyAgent.DeleteLike) => () => agent.DeleteLike(s_spaceRecord, cancellationToken),
            nameof(BlueskyAgent.DeleteRepost) => () => agent.DeleteRepost(s_spaceRecord, cancellationToken),
            nameof(BlueskyAgent.DeleteBlock) => () => agent.DeleteBlock(s_spaceRecord, cancellationToken),
            nameof(BlueskyAgent.DeleteFollow) => () => agent.DeleteFollow(s_spaceRecord, cancellationToken),
            nameof(BlueskyAgent.AddThreadGate) => () => agent.AddThreadGate(s_spaceRecord, cancellationToken: cancellationToken),
            nameof(BlueskyAgent.DeleteThreadGate) => () => agent.DeleteThreadGate(s_spaceRecord, cancellationToken),
            nameof(BlueskyAgent.GetPostGate) => () => agent.GetPostGate(s_spaceRecord, cancellationToken),
            nameof(BlueskyAgent.CreateReferenceListOptOut) => () => agent.CreateReferenceListOptOut(s_spaceRecord, cancellationToken),
            nameof(BlueskyAgent.UpdateList) => () => agent.UpdateList(s_spaceRecord, new List("name", ListPurpose.CurateList, description: null), cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(act);
        Assert.False(serverWasCalled);
    }

    [Fact]
    public async Task SpaceUriParsedThroughTheBaseConstructorIsRejected()
    {
        bool serverWasCalled = false;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            context =>
            {
                serverWasCalled = true;
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Task.CompletedTask;
            });

        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer))
        {
            Credentials = CreateCredentials(),
            Service = TestServerBuilder.DefaultUri
        };

        AtUri uri = new(s_spaceRecord.ToString());

        await Assert.ThrowsAnyAsync<ArgumentException>(() => agent.DeletePost(uri, TestContext.Current.CancellationToken));
        Assert.False(serverWasCalled);
    }
}
