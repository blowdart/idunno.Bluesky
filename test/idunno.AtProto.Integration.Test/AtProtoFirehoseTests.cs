// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.AtProto.Firehose;
using idunno.AtProto.Labels;
using idunno.AtProto.Sync;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;

using static idunno.AtProto.Integration.Test.FirehoseTestData;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class AtProtoFirehoseTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task SubscribeReposYieldsCommitWithOperationsAndRecord()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TestCommit commit = new(7);
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, Frame("#commit", commit.Build()), token));
        await using AtProtoFirehose firehose = CreateFirehose(server);

        List<FirehoseEvent> events = await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken);

        FirehoseCommitEvent commitEvent = Assert.IsType<FirehoseCommitEvent>(Assert.Single(events));
        Assert.Equal(7, commitEvent.Sequence);
        Assert.Equal(new Did(TestDid), commitEvent.Repo);
        Assert.Equal(Revision, commitEvent.Rev);
        Assert.Null(commitEvent.Since);
        Assert.Equal(commit.Data, commitEvent.Data);
        Assert.Equal(commit.Data, commitEvent.PrevData);
        Assert.Equal(new DateTimeOffset(2026, 9, 28, 1, 2, 3, TimeSpan.Zero), commitEvent.Time);
        FirehoseRepoOperation operation = Assert.Single(commitEvent.Operations);
        Assert.Equal(FirehoseRepoAction.Create, operation.Action);
        Assert.Equal(new Nsid("app.bsky.feed.post"), operation.Collection);
        Assert.Equal(TestRecordKey, operation.RecordKey.Value);
        Assert.Equal(commit.RecordCid, operation.Cid);
        Assert.Null(operation.Prev);
        JsonElement? record = operation.GetRecord();
        Assert.NotNull(record);
        Assert.Equal("hello", record.Value.GetProperty("text").GetString());
        Assert.Equal(7, firehose.LastRepoSequence);
        Assert.Equal("/xrpc/com.atproto.sync.subscribeRepos", Assert.Single(server.Connections).Path);
        Assert.Equal(string.Empty, server.Connections.Single().Query);
    }

    [Fact]
    public async Task SubscribeReposDecodesUpdatesAndDeletes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TestCommit commit = new(1);
        Cid previous = CidOf(RecordBytes("before"));
        commit.Operations.Clear();
        commit.Operations.Add(Map(("action", "update"), ("path", $"app.bsky.feed.post/{TestRecordKey}"), ("cid", commit.RecordCid), ("prev", previous)));
        commit.Operations.Add(Map(("action", "delete"), ("path", "app.bsky.feed.like/3lngovum7vm2l"), ("cid", null), ("prev", previous)));
        commit.Operations.Add(Map(("action", "archive"), ("path", "app.bsky.feed.like/3lngovum7vm2m"), ("cid", null)));
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, Frame("#commit", commit.Build()), token));
        await using AtProtoFirehose firehose = CreateFirehose(server);

        FirehoseCommitEvent commitEvent = Assert.IsType<FirehoseCommitEvent>(
            Assert.Single(await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken)));

        Assert.Collection(
            commitEvent.Operations,
            update =>
            {
                Assert.Equal(FirehoseRepoAction.Update, update.Action);
                Assert.Equal(commit.RecordCid, update.Cid);
                Assert.Equal(previous, update.Prev);
                Assert.NotNull(update.RecordData);
            },
            delete =>
            {
                Assert.Equal(FirehoseRepoAction.Delete, delete.Action);
                Assert.Null(delete.Cid);
                Assert.Equal(previous, delete.Prev);
                Assert.Null(delete.RecordData);
                Assert.Null(delete.GetRecord());
            },
            unknown => Assert.Equal(FirehoseRepoAction.Unknown, unknown.Action));
    }

    [Fact]
    public async Task SubscribeReposYieldsSyncIdentityAccountAndInfoEvents()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        byte[] commitBlock = SignedCommit(TestDid, Revision, CidOf(RecordBytes()), new byte[64]);
        Cid commit = CidOf(commitBlock);
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#info", InfoPayload(FirehoseInfoEvent.OutdatedCursor, "cursor is old")), token);
            await Send(socket, Frame("#sync", Map(("seq", 1L), ("did", TestDid), ("rev", Revision), ("time", Time), ("blocks", Car(commit, commitBlock)))), token);
            await Send(socket, Frame("#identity", IdentityPayload(2)), token);
            await Send(socket, Frame("#account", AccountPayload(3, active: false, "takendown")), token);
            await Send(socket, Frame("#account", AccountPayload(4, active: true, "someNewStatus")), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server);

        List<FirehoseEvent> events = await Take(firehose.SubscribeReposAsync(0, cancellationToken: cancellationToken), 5, cancellationToken);

        Assert.Collection(
            events,
            info =>
            {
                FirehoseInfoEvent infoEvent = Assert.IsType<FirehoseInfoEvent>(info);
                Assert.Equal(FirehoseInfoEvent.OutdatedCursor, infoEvent.Name);
                Assert.Equal("cursor is old", infoEvent.Message);
                Assert.Null(infoEvent.Sequence);
            },
            sync =>
            {
                FirehoseSyncEvent syncEvent = Assert.IsType<FirehoseSyncEvent>(sync);
                Assert.Equal(commit, syncEvent.Commit);
                Assert.Equal(CidOf(RecordBytes()), syncEvent.Data);
            },
            identity => Assert.Equal("alice.test", Assert.IsType<FirehoseIdentityEvent>(identity).Handle),
            account =>
            {
                FirehoseAccountEvent accountEvent = Assert.IsType<FirehoseAccountEvent>(account);
                Assert.False(accountEvent.Active);
                Assert.Equal(RepoStatus.Takendown, accountEvent.Status);
            },
            account => Assert.Equal(RepoStatus.Unknown, Assert.IsType<FirehoseAccountEvent>(account).Status));
        Assert.Equal("?cursor=0", server.Connections.Single().Query);
    }

    [Fact]
    public async Task IdentityHandlesAreSanitized()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, Frame("#identity", IdentityPayload(1, handle: "evil\u001b[2J\r\n\u202Etest")), token));
        await using AtProtoFirehose firehose = CreateFirehose(server);

        FirehoseEvent identity = Assert.Single(await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.Equal("evil[2Jtest", Assert.IsType<FirehoseIdentityEvent>(identity).Handle);
    }

    [Fact]
    public void WebSocketProxiesAreRejected()
    {
#pragma warning disable CS0618 // Proxy is obsolete because it is rejected.
        WebSocketOptions webSocketOptions = new() { Proxy = new WebProxy("http://proxy.invalid") };
#pragma warning restore CS0618
        using HttpClient httpClient = new();

        Assert.Equal("webSocketOptions", Assert.Throws<ArgumentException>(() => new AtProtoFirehose(webSocketOptions: webSocketOptions)).ParamName);
        Assert.Equal("webSocketOptions", Assert.Throws<ArgumentException>(() => new AtProtoFirehose(httpClient, webSocketOptions: webSocketOptions)).ParamName);
        Assert.Equal("webSocketOptions", Assert.Throws<ArgumentException>(() => new AtProtoFirehose(new LocalHttpClientFactory(), webSocketOptions: webSocketOptions)).ParamName);
    }

    [Fact]
    public async Task UnknownOperationsAreSkippedAndUnknownTypesSurfaced()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#identity", IdentityPayload(1), operation: 2), token);
            await Send(socket, Frame(null, Map(("anything", 1L)), operation: 7), token);
            await Send(socket, Frame("#brandNew", Map(("seq", 99L), ("extra", "field"))), token);
            await Send(socket, Frame("com.atproto.sync.subscribeRepos#identity", Map(("seq", 2L), ("did", TestDid), ("time", Time), ("unknownField", true))), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server);

        List<FirehoseEvent> events = await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 2, cancellationToken);

        Assert.Equal("#brandNew", Assert.IsType<FirehoseUnknownEvent>(events[0]).Type);
        Assert.Null(events[0].Sequence);
        Assert.Equal(2, Assert.IsType<FirehoseIdentityEvent>(events[1]).Sequence);
    }

    public static TheoryData<string, byte[]> TerminalFrames => new()
    {
        { "not CBOR", [0xFF, 0xFF, 0xFF] },
        { "header only", Encode(Map(("op", 1L), ("t", "#identity"))) },
        { "trailing data", [.. Frame("#identity", IdentityPayload(1)), 0x00] },
        { "non canonical key order", [.. Encode(Map(("op", 1L), ("t", "#identity"))), 0xA2, 0x61, 0x62, 0x01, 0x61, 0x61, 0x02] },
        { "float in payload", [.. Encode(Map(("op", 1L), ("t", "#identity"))), 0xA1, 0x61, 0x61, 0xFB, 0x3F, 0xF0, 0, 0, 0, 0, 0, 0] },
        { "unsupported tag", [.. Encode(Map(("op", 1L), ("t", "#identity"))), 0xA1, 0x61, 0x61, 0xC1, 0x01] },
        { "header without op", [.. Encode(Map(("t", "#identity"))), .. Encode(IdentityPayload(1))] },
        { "message without type", Frame(null, IdentityPayload(1)) },
        { "payload not a map", [.. Encode(Map(("op", 1L), ("t", "#identity"))), .. Encode(new object?[] { 1L })] },
        { "missing sequence", Frame("#identity", Map(("did", TestDid), ("time", Time))) },
        { "zero sequence", Frame("#identity", IdentityPayload(0)) },
        { "sequence of 2^53", Frame("#identity", IdentityPayload(9_007_199_254_740_992)) },
        { "deep nesting", [.. Encode(Map(("op", 1L), ("t", "#identity"))), .. Nested(200)] },
        { "error without error", Frame(null, Map(("message", "no error")), operation: -1) },
    };

    [Theory]
    [MemberData(nameof(TerminalFrames))]
    public async Task InvalidFramingEndsTheStream(string reason, byte[] frame)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, frame, token);
            await Send(socket, Frame("#identity", IdentityPayload(5)), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server);

        await Assert.ThrowsAsync<InvalidDataException>(() => Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));
        Assert.True(server.Connections.Count == 1, reason);
    }

    [Theory]
    [InlineData(5L, "bob.test")]
    [InlineData(4L, "alice.test")]
    public async Task DuplicateOrOutOfOrderSequenceEndsTheStream(long second, string handle)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#identity", IdentityPayload(5)), token);
            await Send(socket, Frame("#identity", IdentityPayload(second, handle: handle)), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server);
        await using IAsyncEnumerator<FirehoseEvent> stream = firehose.SubscribeReposAsync(cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);

        Assert.True(await stream.MoveNextAsync().AsTask().WaitAsync(s_timeout, cancellationToken));
        Assert.Equal(5, stream.Current.Sequence);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await stream.MoveNextAsync());
        Assert.Single(server.Connections);
    }

    [Fact]
    public async Task TextFrameEndsTheStream()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"op\":1}"), WebSocketMessageType.Text, endOfMessage: true, token));
        await using AtProtoFirehose firehose = CreateFirehose(server);

        await Assert.ThrowsAsync<InvalidDataException>(() => Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));
    }

    [Fact]
    public async Task OversizedFrameEndsTheStream()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            byte[] frame = Frame("#identity", Map(("seq", 1L), ("did", TestDid), ("time", Time), ("padding", new string('x', 4096))));

            // Sent in pieces so the limit has to be enforced while the frame is reassembled.
            for (int offset = 0; offset < frame.Length; offset += 512)
            {
                int count = Math.Min(512, frame.Length - offset);
                await socket.SendAsync(frame.AsMemory(offset, count), WebSocketMessageType.Binary, offset + count == frame.Length, token);
            }
        });
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions { MaxMessageSize = 1024, BufferSize = 256 });

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));
        Assert.IsType<WebSocketMessageAbandonedException>(exception.InnerException);
    }

    [Theory]
    [InlineData("valid", "")]
    [InlineData("block cid mismatch", "does not match its content")]
    [InlineData("root is not the commit", "first CAR root")]
    [InlineData("commit did mismatch", "commit DID")]
    [InlineData("commit rev mismatch", "commit revision")]
    [InlineData("record block missing", "record block")]
    [InlineData("blocks too large", "blocks")]
    [InlineData("too many operations", "ops")]
    [InlineData("maximum blobs", "")]
    [InlineData("too many blobs", "blobs")]
    [InlineData("create with prev", "unexpected 'prev'")]
    [InlineData("update with null prev", "unexpected 'prev'")]
    [InlineData("delete with cid", "has a record CID")]
    [InlineData("create without cid", "no record CID")]
    [InlineData("missing cid", "no 'cid' field")]
    [InlineData("hostile car header field", "Unknown CAR header field")]
    [InlineData("invalid path", "path")]
    [InlineData("invalid rev", "TID")]
    [InlineData("missing since", "'since'")]
    [InlineData("missing required field", "'time'")]
    [InlineData("truncated car", "CAR")]
    [InlineData("too many car blocks", "more than 3 blocks")]
    [InlineData("car block too large", "CAR block is too large")]
    public async Task InvalidCommitsAreSurfacedAndTheStreamContinues(string breakage, string reason)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TestCommit commit = new(1);
        Break(commit, breakage);
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#commit", commit.Build()), token);
            await Send(socket, Frame("#identity", IdentityPayload(2)), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions { MaximumCarBlocks = 3, MaximumCarBlockSize = 1024 });

        List<FirehoseEvent> events = await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 2, cancellationToken);

        if (breakage is "valid" or "maximum blobs")
        {
            Assert.IsType<FirehoseCommitEvent>(events[0]);
        }
        else
        {
            FirehoseInvalidEvent invalid = Assert.IsType<FirehoseInvalidEvent>(events[0]);
            Assert.Equal(1, invalid.Sequence);
            Assert.Equal("#commit", invalid.Type);
            Assert.Contains(reason, invalid.Reason, StringComparison.Ordinal);
            Assert.DoesNotContain(invalid.Reason, c => char.IsControl(c) || c == '\u202E');
            Assert.False(invalid.Payload.IsEmpty);
        }

        Assert.IsType<FirehoseIdentityEvent>(events[1]);
        Assert.Single(server.Connections);
    }

    [Theory]
    [InlineData("valid", "")]
    [InlineData("blocks too large", "blocks")]
    [InlineData("commit did mismatch", "commit DID")]
    [InlineData("root block missing", "commit block is missing")]
    public async Task InvalidSyncEventsAreSurfaced(string breakage, string reason)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        byte[] commitBlock = SignedCommit(breakage == "commit did mismatch" ? OtherDid : TestDid, Revision, CidOf(RecordBytes()), new byte[64]);
        Cid commit = CidOf(commitBlock);
        byte[] car = breakage switch
        {
            "blocks too large" => Car(commit, commitBlock, new byte[10_000]),
            "root block missing" => Car(CidOf(RecordBytes()), commitBlock),
            _ => Car(commit, commitBlock)
        };
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
            await Send(socket, Frame("#sync", Map(("seq", 1L), ("did", TestDid), ("rev", Revision), ("time", Time), ("blocks", car))), token));
        await using AtProtoFirehose firehose = CreateFirehose(server);

        FirehoseEvent syncEvent = Assert.Single(await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        if (breakage == "valid")
        {
            Assert.IsType<FirehoseSyncEvent>(syncEvent);
        }
        else
        {
            FirehoseInvalidEvent invalid = Assert.IsType<FirehoseInvalidEvent>(syncEvent);
            Assert.Equal(new Did(TestDid), invalid.Did);
            Assert.Contains(reason, invalid.Reason, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(true, false, typeof(FirehoseCommitEvent))]
    [InlineData(true, true, typeof(FirehoseInvalidEvent))]
    [InlineData(false, true, typeof(FirehoseCommitEvent))]
    public async Task CommitSignaturesAreVerifiedWhenEnabled(bool verify, bool corrupt, Type expected)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DidDocument document = DidDocumentFor(TestDid, "atproto", key);
        TestCommit commit = new(1) { SigningKey = key, CorruptSignature = corrupt };
        int resolutions = 0;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, Frame("#commit", commit.Build()), token));
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions
        {
            VerifySignatures = verify,
            DidDocumentResolver = (did, _) =>
            {
                Interlocked.Increment(ref resolutions);
                return Task.FromResult<DidDocument?>(did == new Did(TestDid) ? document : null);
            }
        });

        FirehoseEvent commitEvent = Assert.Single(await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.IsType(expected, commitEvent);
        Assert.Equal(verify ? 1 : 0, resolutions);
    }

    [Fact]
    public async Task CommitSignatureVerificationFailsWhenTheDidCannotBeResolved()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, Frame("#commit", new TestCommit(1).Build()), token));
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions
        {
            VerifySignatures = true,
            DidDocumentResolver = (_, _) => throw new HttpRequestException("resolution failed")
        });

        FirehoseEvent commitEvent = Assert.Single(await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.IsType<FirehoseInvalidEvent>(commitEvent);
    }

    [Fact]
    public async Task SubscribeLabelsYieldsLabels()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#labels", LabelsPayload(3, LabelPayload("spam"), LabelPayload("porn"))), token);
            await Send(socket, Frame("#info", InfoPayload("SomethingNew")), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server);

        List<FirehoseEvent> events = await Take(firehose.SubscribeLabelsAsync(2, cancellationToken: cancellationToken), 2, cancellationToken);

        FirehoseLabelsEvent labels = Assert.IsType<FirehoseLabelsEvent>(events[0]);
        Assert.Equal(3, labels.Sequence);
        Assert.Collection(
            labels.Labels,
            label =>
            {
                Assert.Equal("spam", label.Value);
                Assert.Equal(new Did(OtherDid), label.Source);
                Assert.Equal($"at://{TestDid}/app.bsky.feed.post/{TestRecordKey}", label.Uri);
                Assert.Equal(CidOf(RecordBytes()), label.Cid);
                Assert.Equal(1, label.Version);
                Assert.False(label.IsNegationLabel);
                Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), label.ExpiresAt);
                Assert.Equal(64, label.Signature!.Count());
            },
            label => Assert.Equal("porn", label.Value));
        Assert.Equal("SomethingNew", Assert.IsType<FirehoseInfoEvent>(events[1]).Name);
        Assert.Equal(3, firehose.LastLabelSequence);
        Assert.Null(firehose.LastRepoSequence);
        Assert.Equal("/xrpc/com.atproto.label.subscribeLabels", server.Connections.Single().Path);
        Assert.Equal("?cursor=2", server.Connections.Single().Query);
    }

    [Fact]
    public async Task LabelFloodIsSurfacedAsInvalid()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#labels", LabelsPayload(1, LabelPayload(), LabelPayload(), LabelPayload())), token);
            await Send(socket, Frame("#labels", LabelsPayload(2, LabelPayload(), LabelPayload())), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions { MaximumLabelsPerMessage = 2 });

        List<FirehoseEvent> events = await Take(firehose.SubscribeLabelsAsync(cancellationToken: cancellationToken), 2, cancellationToken);

        Assert.IsType<FirehoseInvalidEvent>(events[0]);
        Assert.Equal(2, Assert.IsType<FirehoseLabelsEvent>(events[1]).Labels.Count);
    }

    [Theory]
    [InlineData(false, false, typeof(FirehoseLabelsEvent))]
    [InlineData(true, false, typeof(FirehoseInvalidEvent))]
    [InlineData(false, true, typeof(FirehoseInvalidEvent))]
    public async Task LabelSignaturesAreVerifiedWhenEnabled(bool corrupt, bool unsigned, Type expected)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DidDocument document = DidDocumentFor(OtherDid, "atproto_label", key);
        Dictionary<string, object?> label = LabelPayload(key: key, corruptSignature: corrupt);

        if (unsigned)
        {
            label.Remove("sig");
        }

        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, Frame("#labels", LabelsPayload(1, label)), token));
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions
        {
            VerifySignatures = true,
            DidDocumentResolver = (_, _) => Task.FromResult<DidDocument?>(document)
        });

        FirehoseEvent labelsEvent = Assert.Single(await Take(firehose.SubscribeLabelsAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.IsType(expected, labelsEvent);
    }

    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 2)]
    [InlineData(false, false, 3)]
    [InlineData(false, true, 3)]
    public async Task SigningKeysAreCachedUnlessDisabled(bool cache, bool identityChange, int expectedResolutions)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DidDocument document = DidDocumentFor(TestDid, "atproto", key);
        int resolutions = 0;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#commit", new TestCommit(1) { SigningKey = key }.Build()), token);

            if (identityChange)
            {
                await Send(socket, Frame("#identity", IdentityPayload(2)), token);
            }

            await Send(socket, Frame("#commit", new TestCommit(3) { SigningKey = key }.Build()), token);
            await Send(socket, Frame("#commit", new TestCommit(4) { SigningKey = key }.Build()), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions
        {
            VerifySignatures = true,
            CacheSigningKeys = cache,
            DidDocumentResolver = (_, _) =>
            {
                Interlocked.Increment(ref resolutions);
                return Task.FromResult<DidDocument?>(document);
            }
        });

        List<FirehoseEvent> events = await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), identityChange ? 4 : 3, cancellationToken);

        Assert.Equal(3, events.OfType<FirehoseCommitEvent>().Count());
        Assert.Equal(expectedResolutions, resolutions);
    }

    [Fact]
    public async Task LabelSigningKeysAreCachedAcrossMessages()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DidDocument document = DidDocumentFor(OtherDid, "atproto_label", key);
        int resolutions = 0;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#labels", LabelsPayload(1, LabelPayload("spam", key), LabelPayload("porn", key))), token);
            await Send(socket, Frame("#labels", LabelsPayload(2, LabelPayload("spam", key))), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions
        {
            VerifySignatures = true,
            DidDocumentResolver = (_, _) =>
            {
                Interlocked.Increment(ref resolutions);
                return Task.FromResult<DidDocument?>(document);
            }
        });

        List<FirehoseEvent> events = await Take(firehose.SubscribeLabelsAsync(cancellationToken: cancellationToken), 2, cancellationToken);

        Assert.All(events, labelsEvent => Assert.IsType<FirehoseLabelsEvent>(labelsEvent));
        Assert.Equal(1, resolutions);
    }

    [Theory]
    [InlineData(1, 1, typeof(FirehoseLabelsEvent), 1)]
    [InlineData(3, 3, typeof(FirehoseLabelsEvent), 3)]
    [InlineData(3, 4, typeof(FirehoseInvalidEvent), 0)]
    [InlineData(1, 2, typeof(FirehoseInvalidEvent), 0)]
    public async Task LabelSourcesAreCappedBeforeAnyAreResolved(int maximumSources, int sources, Type expected, int expectedResolutions)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Dictionary<string, object?>[] labels =
        [
            .. Enumerable.Range(0, sources).SelectMany(i => new[]
            {
                LabelPayload("spam", key, source: $"did:web:source{i}.example"),
                LabelPayload("porn", key, source: $"did:web:source{i}.example")
            })
        ];
        int resolutions = 0;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, Frame("#labels", LabelsPayload(1, labels)), token));
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions
        {
            VerifySignatures = true,
            MaximumLabelSourcesPerMessage = maximumSources,
            DidDocumentResolver = (did, _) =>
            {
                Interlocked.Increment(ref resolutions);
                return Task.FromResult<DidDocument?>(DidDocumentFor(did.Value, "atproto_label", key));
            }
        });

        FirehoseEvent labelsEvent = Assert.Single(await Take(firehose.SubscribeLabelsAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.IsType(expected, labelsEvent);
        Assert.Equal(expectedResolutions, resolutions);
    }

    [Fact]
    public async Task LabelSourcesAreNotCappedWithoutVerification()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(
            socket,
            Frame("#labels", LabelsPayload(1, LabelPayload(source: "did:web:one.example"), LabelPayload(source: "did:web:two.example"))),
            token));
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions { MaximumLabelSourcesPerMessage = 1 });

        FirehoseEvent labelsEvent = Assert.Single(await Take(firehose.SubscribeLabelsAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.Equal(2, Assert.IsType<FirehoseLabelsEvent>(labelsEvent).Labels.Count);
    }

    [Fact]
    public async Task ErrorFramesEndTheStreamWithTheSanitizedError()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, ErrorFrame("FutureCursor", "Cursor\u001b[31m in the future" + new string('x', 2000)), token));
        await using AtProtoFirehose firehose = CreateFirehose(server);

        FirehoseConnectionException exception = await Assert.ThrowsAsync<FirehoseConnectionException>(
            () => Take(firehose.SubscribeReposAsync(long.MaxValue / 2048, cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.Equal("FutureCursor", exception.ErrorDetail?.Error);
        Assert.StartsWith("Cursor[31m in the future", exception.ErrorDetail?.Message, StringComparison.Ordinal);
        Assert.Equal(1024, exception.ErrorDetail?.Message?.Length);
        Assert.Null(exception.StatusCode);
        Assert.Single(server.Connections);
    }

    [Fact]
    public async Task ConsumerTooSlowReconnectsFromTheLastSequence()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, connection, token) =>
        {
            if (connection == 1)
            {
                await Send(socket, Frame("#identity", IdentityPayload(10)), token);
                await Send(socket, ErrorFrame("ConsumerTooSlow"), token);
            }
            else
            {
                await Send(socket, Frame("#identity", IdentityPayload(10)), token);
                await Send(socket, Frame("#identity", IdentityPayload(11)), token);
            }
        });
        await using AtProtoFirehose firehose = CreateFirehose(server);

        List<FirehoseEvent> events = await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 2, cancellationToken);

        Assert.Equal([10L, 11L], events.Select(e => e.Sequence!.Value));
        Assert.Equal(["", "?cursor=10"], server.Connections.Select(c => c.Query));
    }

    [Fact]
    public async Task ResumesAfterTheServerClosesWithoutDuplicates()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, connection, token) =>
        {
            if (connection == 1)
            {
                await Send(socket, Frame("#identity", IdentityPayload(21)), token);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
            }
            else
            {
                // A labeler treats the cursor as exclusive, so it does not repeat 21.
                await Send(socket, Frame("#identity", IdentityPayload(22)), token);
            }
        });
        await using AtProtoFirehose firehose = CreateFirehose(server);

        List<FirehoseEvent> events = await Take(firehose.SubscribeReposAsync(20, cancellationToken: cancellationToken), 2, cancellationToken);

        Assert.Equal([21L, 22L], events.Select(e => e.Sequence!.Value));
        Assert.Equal(["?cursor=20", "?cursor=21"], server.Connections.Select(c => c.Query));
    }

    [Fact]
    public async Task OnlyTheFirstFrameOfAResumedConnectionMayRepeatTheCursor()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#identity", IdentityPayload(30)), token);
            await Send(socket, Frame("#identity", IdentityPayload(30, handle: "bob.test")), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server);

        await Assert.ThrowsAsync<InvalidDataException>(() => Take(firehose.SubscribeReposAsync(30, cancellationToken: cancellationToken), 1, cancellationToken));
    }

    [Fact]
    public async Task ARepeatOfTheLastEventAtTheSwitchToLiveEventsIsDropped()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            // As a relay does when resuming: the cursor event, the replayed events, the last of them again, then live events.
            await Send(socket, Frame("#identity", IdentityPayload(30)), token);
            await Send(socket, Frame("#identity", IdentityPayload(31)), token);
            await Send(socket, Frame("#identity", IdentityPayload(32)), token);
            await Send(socket, Frame("#identity", IdentityPayload(32)), token);
            await Send(socket, Frame("#identity", IdentityPayload(33)), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server);

        List<FirehoseEvent> events = await Take(firehose.SubscribeReposAsync(30, cancellationToken: cancellationToken), 3, cancellationToken);

        Assert.Equal([31L, 32L, 33L], events.Select(e => e.Sequence!.Value));
        Assert.Single(server.Connections);
    }

    [Fact]
    public async Task ARepeatedSequenceWithTheSamePayloadButADifferentTypeEndsTheStream()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#identity", IdentityPayload(5)), token);
            await Send(socket, Frame("#account", IdentityPayload(5)), token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server);

        await Assert.ThrowsAsync<InvalidDataException>(() => Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 2, cancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(5L)]
    public async Task LastSequenceIsTheStartingCursorUntilAnEventIsYielded(long? cursor)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, Frame("#info", Map(("name", "Hello"))), token));
        await using AtProtoFirehose firehose = CreateFirehose(server);

        List<FirehoseEvent> events = await Take(firehose.SubscribeReposAsync(cursor, cancellationToken: cancellationToken), 1, cancellationToken);

        Assert.IsType<FirehoseInfoEvent>(Assert.Single(events));
        Assert.Equal(cursor, firehose.LastRepoSequence);
    }
    [Fact]
    public async Task ClosingWaitsForTheServerToAnswerTheCloseHandshake()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TimeSpan replyDelay = TimeSpan.FromMilliseconds(500);
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#identity", IdentityPayload(1)), token);

            byte[] buffer = new byte[1024];
            while ((await socket.ReceiveAsync(buffer, token)).MessageType != WebSocketMessageType.Close)
            {
            }

            await Task.Delay(replyDelay, token);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions { CloseTimeout = TimeSpan.FromSeconds(30) });

        IAsyncEnumerator<FirehoseEvent> events = firehose.SubscribeReposAsync(cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await events.MoveNextAsync().AsTask().WaitAsync(s_timeout, cancellationToken));

        Stopwatch stopwatch = Stopwatch.StartNew();
        await events.DisposeAsync().AsTask().WaitAsync(s_timeout, cancellationToken);

        Assert.True(stopwatch.Elapsed >= replyDelay - TimeSpan.FromMilliseconds(50), $"Closing took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task ClosingIsAbortedWhenTheServerDoesNotAnswerTheCloseHandshake()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TimeSpan closeTimeout = TimeSpan.FromMilliseconds(300);
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#identity", IdentityPayload(1)), token);

            // Never answer the close, holding the connection open until the server is disposed.
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions { CloseTimeout = closeTimeout });

        IAsyncEnumerator<FirehoseEvent> events = firehose.SubscribeReposAsync(cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await events.MoveNextAsync().AsTask().WaitAsync(s_timeout, cancellationToken));

        Stopwatch stopwatch = Stopwatch.StartNew();
        await events.DisposeAsync().AsTask().WaitAsync(s_timeout, cancellationToken);

        Assert.True(stopwatch.Elapsed >= closeTimeout - TimeSpan.FromMilliseconds(50), $"Closing took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task IdleConnectionsAreReconnected()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, connection, token) =>
        {
            if (connection > 1)
            {
                await Send(socket, Frame("#identity", IdentityPayload(1)), token);
            }
        });
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions { IdleTimeout = TimeSpan.FromMilliseconds(250) });

        FirehoseEvent identity = Assert.Single(await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.Equal(1, identity.Sequence);
        Assert.Equal(2, server.Connections.Count);
    }

    [Fact]
    public async Task RetryableRefusalsHonourRetryAfterAndReconnect()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer
        {
            RefuseWith = attempt => attempt == 1 ? (HttpStatusCode.TooManyRequests, "0", "<html>slow down</html>") : null
        };
        server.Start(async (socket, _, token) => await Send(socket, Frame("#identity", IdentityPayload(1)), token));
        await using AtProtoFirehose firehose = CreateFirehose(server);

        FirehoseEvent identity = Assert.Single(await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.Equal(1, identity.Sequence);
        Assert.Equal(2, server.UpgradeAttempts);
    }

    [Fact]
    public async Task RefusedUpgradesAreNotCountedAsClosedConnections()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer
        {
            RefuseWith = attempt => attempt <= 2 ? (HttpStatusCode.ServiceUnavailable, "0", "<html>unavailable</html>") : null
        };
        server.Start(async (socket, _, token) => await Send(socket, Frame("#identity", IdentityPayload(1)), token));
        await using AtProtoFirehose firehose = CreateFirehose(server);
        using MetricCollector<long> opened = new(firehose.Metrics.ConnectionsOpened);
        using MetricCollector<long> closed = new(firehose.Metrics.ConnectionsClosed);

        Assert.Single(await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        string authority = server.Uri.GetLeftPart(UriPartial.Authority);
        Assert.Equal(3, server.UpgradeAttempts);
        Assert.Equal(1, ForServer(opened, authority));
        Assert.Equal(1, ForServer(closed, authority));

        static long ForServer(MetricCollector<long> collector, string authority) =>
            collector.GetMeasurementSnapshot().Where(m => Equals(m.Tags["server"], authority)).Sum(m => m.Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotImplemented)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.OK)]
    public async Task NonRetryableRefusalsThrow(HttpStatusCode statusCode)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer { RefuseWith = _ => (statusCode, "120", "<html>no</html>") };
        server.Start((_, _, _) => Task.CompletedTask);
        await using AtProtoFirehose firehose = CreateFirehose(server);

        FirehoseConnectionException exception = await Assert.ThrowsAsync<FirehoseConnectionException>(
            () => Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(120), exception.RetryAfter);
        Assert.Equal(1, server.UpgradeAttempts);
    }

    [Fact]
    public async Task ReconnectionGivesUpAfterTheMaximumAttempts()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer { RefuseWith = _ => (HttpStatusCode.ServiceUnavailable, null, "unavailable") };
        server.Start((_, _, _) => Task.CompletedTask);
        await using AtProtoFirehose firehose = CreateFirehose(server);

        IOException exception = await Assert.ThrowsAsync<IOException>(
            () => Take(firehose.SubscribeReposAsync(maximumReconnectAttempts: 2, cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, Assert.IsType<FirehoseConnectionException>(exception.InnerException).StatusCode);
        Assert.Equal(3, server.UpgradeAttempts);
    }

    [Fact]
    public async Task UnsequencedEventsDoNotResetTheReconnectionCount()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) =>
        {
            await Send(socket, Frame("#info", InfoPayload("SomethingNew")), token);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
        });
        await using AtProtoFirehose firehose = CreateFirehose(server);

        await Assert.ThrowsAsync<IOException>(
            () => Take(firehose.SubscribeReposAsync(maximumReconnectAttempts: 2, cancellationToken: cancellationToken), 100, cancellationToken));

        Assert.Equal(3, server.Connections.Count);
    }

    [Fact]
    public async Task DisposingDuringSignatureVerificationCancelsTheEnumeration()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DidDocument document = DidDocumentFor(TestDid, "atproto", key);
        TaskCompletionSource resolving = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<DidDocument?> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, Frame("#commit", new TestCommit(1) { SigningKey = key }.Build()), token));
        await using AtProtoFirehose firehose = CreateFirehose(server, new FirehoseOptions
        {
            VerifySignatures = true,
            DidDocumentResolver = (_, _) =>
            {
                // Ignores its cancellation token, as a resolver is free to, so it finishes after the firehose has been disposed.
                resolving.TrySetResult();
                return release.Task;
            }
        });

        Task<List<FirehoseEvent>> reading = Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken);
        await resolving.Task.WaitAsync(cancellationToken);
        await firehose.DisposeAsync();
        release.SetResult(document);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public async Task RedirectsAreNotFollowedOrRetried(HttpStatusCode statusCode)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var target = new TestFirehoseServer();
        target.Start(async (socket, _, token) => await Send(socket, Frame("#identity", IdentityPayload(1)), token));
        using var server = new TestFirehoseServer { RedirectWith = (statusCode, target.Uri) };
        server.Start((_, _, _) => Task.CompletedTask);
        await using AtProtoFirehose firehose = CreateFirehose(server, httpClientFactory: new LocalHttpClientFactory(allowAutoRedirect: false));

        FirehoseConnectionException exception = await Assert.ThrowsAsync<FirehoseConnectionException>(
            () => Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Contains("redirected", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, server.UpgradeAttempts);
        Assert.Empty(target.Connections);
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public async Task RedirectsFollowedByASuppliedClientAreRejected(HttpStatusCode statusCode)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var target = new TestFirehoseServer();
        target.Start(async (socket, _, token) => await Send(socket, Frame("#identity", IdentityPayload(1)), token));
        using var server = new TestFirehoseServer { RedirectWith = (statusCode, target.Uri) };
        server.Start((_, _, _) => Task.CompletedTask);
        using HttpClient httpClient = new();
        await using AtProtoFirehose firehose = CreateFirehose(server, httpClient: httpClient);

        FirehoseConnectionException exception = await Assert.ThrowsAsync<FirehoseConnectionException>(
            () => Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));

        Assert.Null(exception.StatusCode);
        Assert.Contains("redirected", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, server.UpgradeAttempts);

        // The client followed the redirect before the firehose could see it, which is the risk its documentation warns of,
        // but the connection it made was never read from.
        Assert.Single(target.Connections);
    }

    [Fact]
    public async Task SuppliedHttpClientIsUsedAndNotDisposed()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, Frame("#identity", IdentityPayload(1)), token));
        using TrackingHandler handler = new();
        using HttpClient httpClient = new(handler, disposeHandler: false);

        await using (AtProtoFirehose firehose = CreateFirehose(server, httpClient: httpClient))
        {
            List<FirehoseEvent> events = await Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken);

            Assert.IsType<FirehoseIdentityEvent>(Assert.Single(events));
        }

        Assert.Equal(1, handler.Requests);
        Assert.False(handler.Disposed);
    }

    [Fact]
    public void NullHttpClientsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new AtProtoFirehose((HttpClient)null!));
        Assert.Throws<ArgumentNullException>(() => new AtProtoFirehose((IHttpClientFactory)null!));
    }

    [Fact]
    public async Task OnlyOneEnumerationOfAnEndpointCanBeActive()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestFirehoseServer();
        server.Start(async (socket, _, token) => await Send(socket, Frame("#identity", IdentityPayload(1)), token));
        await using AtProtoFirehose firehose = CreateFirehose(server);
        await using IAsyncEnumerator<FirehoseEvent> first = firehose.SubscribeReposAsync(cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await first.MoveNextAsync().AsTask().WaitAsync(s_timeout, cancellationToken));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Take(firehose.SubscribeReposAsync(cancellationToken: cancellationToken), 1, cancellationToken));
    }

    [Theory]
    [InlineData(-1L, 10)]
    [InlineData(9_007_199_254_740_992L, 10)]
    [InlineData(null, -1)]
    public void InvalidArgumentsAreRejected(long? cursor, int attempts)
    {
        using AtProtoFirehose firehose = new(new LocalHttpClientFactory());

        Assert.Throws<ArgumentOutOfRangeException>(() => firehose.SubscribeReposAsync(cursor, attempts, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => firehose.SubscribeLabelsAsync(cursor, attempts, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DisposedFirehoseCannotSubscribe()
    {
        AtProtoFirehose firehose = new(new LocalHttpClientFactory());
        firehose.Dispose();

        Assert.Throws<ObjectDisposedException>(() => firehose.SubscribeReposAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    private static void Break(TestCommit commit, string breakage)
    {
        switch (breakage)
        {
            case "valid":
                break;
            case "block cid mismatch":
                commit.Blocks.Add((CidOf(RecordBytes("claimed")), RecordBytes("actual")));
                break;
            case "root is not the commit":
                commit.Root = commit.RecordCid;
                break;
            case "commit did mismatch":
                commit.CommitDid = OtherDid;
                break;
            case "commit rev mismatch":
                commit.CommitRev = OtherRevision;
                break;
            case "record block missing":
                commit.IncludeRecordBlock = false;
                break;
            case "blocks too large":
                commit.Configure = payload => payload["blocks"] = new byte[2_000_001];
                break;
            case "too many operations":
                commit.Operations.AddRange(Enumerable.Repeat(commit.Operations[0], 200));
                break;
            case "maximum blobs":
                commit.Configure = payload => payload["blobs"] = Enumerable.Repeat<object?>(commit.RecordCid, 200).ToArray();
                break;
            case "too many blobs":
                commit.Configure = payload => payload["blobs"] = Enumerable.Repeat<object?>(commit.RecordCid, 201).ToArray();
                break;
            case "create with prev":
                commit.Operations[0]["prev"] = commit.RecordCid;
                break;
            case "update with null prev":
                commit.Operations[0]["action"] = "update";
                commit.Operations[0]["prev"] = null;
                break;
            case "delete with cid":
                commit.Operations[0]["action"] = "delete";
                break;
            case "create without cid":
                commit.Operations[0]["cid"] = null;
                break;
            case "missing cid":
                commit.Operations[0].Remove("cid");
                break;
            case "hostile car header field":
                commit.Configure = payload => payload["blocks"] = CarHeader(("roots", Array.Empty<object?>()), ("\u202Ex\n", 1L));
                break;
            case "invalid path":
                commit.Operations[0]["path"] = "app.bsky.feed.post/a/b";
                break;
            case "invalid rev":
                commit.Rev = "not-a-tid";
                commit.CommitRev = "not-a-tid";
                break;
            case "missing since":
                commit.Configure = payload => payload.Remove("since");
                break;
            case "missing required field":
                commit.Configure = payload => payload.Remove("time");
                break;
            case "truncated car":
                commit.Configure = payload => payload["blocks"] = ((byte[])payload["blocks"]!)[..^3];
                break;
            case "too many car blocks":
                commit.Blocks.Add((CidOf(RecordBytes("a")), RecordBytes("a")));
                commit.Blocks.Add((CidOf(RecordBytes("b")), RecordBytes("b")));
                break;
            case "car block too large":
                byte[] large = RecordBytes(new string('x', 2048));
                commit.Blocks.Add((CidOf(large), large));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(breakage), breakage, null);
        }
    }

    private static byte[] Nested(int depth)
    {
        byte[] nested = new byte[depth + 1];
        Array.Fill(nested, (byte)0x81, 0, depth);
        nested[depth] = 0x01;
        return [0xA1, 0x61, 0x61, .. nested];
    }

    private static AtProtoFirehose CreateFirehose(
        TestFirehoseServer server,
        FirehoseOptions? options = null,
        IHttpClientFactory? httpClientFactory = null,
        HttpClient? httpClient = null)
    {
        options ??= new FirehoseOptions();
        options = options with { RelayUri = server.Uri, LabelerUri = server.Uri };
        AtProtoFirehose firehose = httpClient is null
            ? new(httpClientFactory ?? new LocalHttpClientFactory(), options)
            : new(httpClient, options);

        foreach (EventStreamReader reader in new[] { firehose.RepoReader, firehose.LabelReader })
        {
            reader.InitialReconnectDelay = TimeSpan.FromMilliseconds(10);
            reader.MaximumReconnectDelay = TimeSpan.FromMilliseconds(50);
        }

        return firehose;
    }

    private static async Task<List<FirehoseEvent>> Take(IAsyncEnumerable<FirehoseEvent> stream, int count, CancellationToken cancellationToken)
    {
        List<FirehoseEvent> events = [];

        await using IAsyncEnumerator<FirehoseEvent> enumerator = stream.GetAsyncEnumerator(cancellationToken);

        while (events.Count < count)
        {
            if (!await enumerator.MoveNextAsync().AsTask().WaitAsync(s_timeout, cancellationToken))
            {
                break;
            }

            events.Add(enumerator.Current);
        }

        return events;
    }

    private static Task Send(WebSocket socket, byte[] frame, CancellationToken cancellationToken) =>
        socket.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);

    private sealed class LocalHttpClientFactory(bool allowAutoRedirect = true) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new SocketsHttpHandler { AllowAutoRedirect = allowAutoRedirect });
    }

    private sealed class TrackingHandler : DelegatingHandler
    {
        private int _requests;

        public TrackingHandler() : base(new SocketsHttpHandler())
        {
        }

        public int Requests => Volatile.Read(ref _requests);

        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            return base.SendAsync(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TestFirehoseServer : IDisposable
    {
        private HttpListener _listener = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly ConcurrentQueue<Connection> _connections = new();
        private int _connectionCount;
        private int _upgradeAttempts;

        public sealed record Connection(string Path, string Query);

        public Uri Uri { get; private set; } = new("ws://localhost");

        /// <summary>
        /// Decides, from the number of the upgrade attempt, whether to refuse it, and with what status, <c>Retry-After</c> and body.
        /// </summary>
        public Func<int, (HttpStatusCode StatusCode, string? RetryAfter, string Body)?>? RefuseWith { get; init; }

        /// <summary>
        /// Redirects every upgrade attempt, with the status and to the location given.
        /// </summary>
        public (HttpStatusCode StatusCode, Uri Location)? RedirectWith { get; init; }

        public IReadOnlyList<Connection> Connections => [.. _connections];

        public int UpgradeAttempts => Volatile.Read(ref _upgradeAttempts);

        public void Start(Func<WebSocket, int, CancellationToken, Task> onConnected)
        {
            StartListener();

            _ = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    HttpListenerContext context;

                    try
                    {
                        context = await _listener.GetContextAsync();
                    }
                    catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
                    {
                        return;
                    }

                    if (!context.Request.IsWebSocketRequest)
                    {
                        await Respond(context, HttpStatusCode.UpgradeRequired, null, "upgrade required");
                        continue;
                    }

                    if (RedirectWith is (HttpStatusCode redirectStatus, Uri location))
                    {
                        Interlocked.Increment(ref _upgradeAttempts);
                        context.Response.RedirectLocation = new Uri(location, context.Request.Url?.PathAndQuery ?? "/").ToString();
                        await Respond(context, redirectStatus, null, string.Empty);
                        continue;
                    }

                    (HttpStatusCode StatusCode, string? RetryAfter, string Body)? refusal = RefuseWith?.Invoke(Interlocked.Increment(ref _upgradeAttempts));

                    if (refusal is not null)
                    {
                        await Respond(context, refusal.Value.StatusCode, refusal.Value.RetryAfter, refusal.Value.Body);
                        continue;
                    }

                    // Recorded before the upgrade is accepted, so a client which has seen the upgrade complete always finds it recorded.
                    _connections.Enqueue(new Connection(context.Request.Url?.AbsolutePath ?? string.Empty, context.Request.Url?.Query ?? string.Empty));
                    HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
                    int connectionNumber = Interlocked.Increment(ref _connectionCount);

                    _ = Task.Run(async () =>
                    {
                        using WebSocket webSocket = webSocketContext.WebSocket;

                        try
                        {
                            await onConnected(webSocket, connectionNumber, _cancellationTokenSource.Token);

                            byte[] buffer = new byte[1024];

                            while (webSocket.State == WebSocketState.Open && !_cancellationTokenSource.IsCancellationRequested)
                            {
                                WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, _cancellationTokenSource.Token);

                                if (result.MessageType == WebSocketMessageType.Close)
                                {
                                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, _cancellationTokenSource.Token);
                                    break;
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (WebSocketException)
                        {
                        }
                    }, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

        private void StartListener()
        {
            const int maximumAttempts = 20;

            for (int attempt = 1; ; attempt++)
            {
                int port = FreePort();

                // A listener that fails to start disposes itself, so each attempt needs a new one.
                if (attempt > 1)
                {
                    _listener = new HttpListener();
                }

                _listener.Prefixes.Add(string.Create(CultureInfo.InvariantCulture, $"http://localhost:{port}/"));

                try
                {
                    _listener.Start();
                }
                catch (HttpListenerException) when (attempt < maximumAttempts)
                {
                    continue;
                }

                Uri = new Uri(string.Create(CultureInfo.InvariantCulture, $"ws://localhost:{port}"));
                return;
            }
        }

        private static int FreePort()
        {
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task Respond(HttpListenerContext context, HttpStatusCode statusCode, string? retryAfter, string body)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = (int)statusCode;
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = bytes.Length;

                if (retryAfter is not null)
                {
                    context.Response.Headers["Retry-After"] = retryAfter;
                }

                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
            catch (HttpListenerException)
            {
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                _listener.Abort();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (HttpListenerException)
            {
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }
        }
    }
}
