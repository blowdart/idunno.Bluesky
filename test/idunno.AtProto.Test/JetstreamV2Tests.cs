// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.Globalization;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Events;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class JetstreamV2Tests
{
    private const string TestDid = "did:plc:g6ylltenitt4tp27bpwalh7b";
    private const string TypePrefix = "network.bsky.jetstream.subscribeEvents#";

    private static readonly Uri s_server = new("wss://jetstream.example.com");

    private static AtProtoJetstream CreateJetstream(JetstreamProtocolVersion protocolVersion = JetstreamProtocolVersion.V2, bool useCompression = false) =>
        new(uri: s_server, options: new JetstreamOptions { ProtocolVersion = protocolVersion, UseCompression = useCompression, MaxMessageSize = 1024 });

    private static string Message(string kind, string body, long sequence = 42) =>
        $$$"""
        {"$type":"message","payload":{"$type":"{{{TypePrefix}}}{{{kind}}}","did":"{{{TestDid}}}","seq":{{{sequence}}},"time":"2026-09-26T00:19:35.411026Z","witnessedAt":"2026-09-26T00:19:35.5Z"{{{body}}}}}
        """;

    [Fact]
    public void TheDefaultProtocolVersionIsV2()
    {
        Assert.Equal(JetstreamProtocolVersion.V2, new JetstreamOptions().ProtocolVersion);
        Assert.Equal(JetstreamProtocolVersion.V2, AtProtoJetstreamBuilder.Create().ProtocolVersion);
    }

    [Theory]
    [InlineData(JetstreamProtocolVersion.V1, "wss://jetstream1.us-west.bsky.network/")]
    [InlineData(JetstreamProtocolVersion.V2, "wss://jetstream.us-west.bsky.network/")]
    public void TheBuilderDefaultServiceMatchesTheProtocolVersion(JetstreamProtocolVersion protocolVersion, string expected)
    {
        AtProtoJetstreamBuilder builder = AtProtoJetstreamBuilder.Create().UseProtocolVersion(protocolVersion);

        Assert.Equal(new Uri(expected), builder.Service);
    }

    [Fact]
    public void AnExplicitServiceIsKeptWhenTheProtocolVersionChanges()
    {
        AtProtoJetstreamBuilder builder = AtProtoJetstreamBuilder.Create()
            .ConnectTo(s_server)
            .UseProtocolVersion(JetstreamProtocolVersion.V1);

        Assert.Equal(s_server, builder.Service);
    }

    [Fact]
    public void AnUndefinedProtocolVersionIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JetstreamOptions { ProtocolVersion = (JetstreamProtocolVersion)3 });
        Assert.Throws<ArgumentOutOfRangeException>(() => AtProtoJetstreamBuilder.Create().UseProtocolVersion((JetstreamProtocolVersion)0));
    }

    [Fact]
    public void TheBuilderAppliesTheKindFilter()
    {
        using AtProtoJetstream jetstream = AtProtoJetstreamBuilder.Create()
            .FilterTo([JetStreamEventKind.Identity, JetStreamEventKind.Sync, JetStreamEventKind.Identity])
            .Build();

        Assert.Equal([JetStreamEventKind.Identity, JetStreamEventKind.Sync], jetstream.KindFilter);
    }

    [Fact]
    public void TheBuilderRejectsAKindFilterForV1()
    {
        AtProtoJetstreamBuilder builder = AtProtoJetstreamBuilder.Create()
            .UseProtocolVersion(JetstreamProtocolVersion.V1)
            .FilterTo([JetStreamEventKind.Identity]);

        Assert.Throws<NotSupportedException>(builder.Build);
    }

    [Fact]
    public void AKindFilterIsNotSupportedByV1()
    {
        using AtProtoJetstream jetstream = CreateJetstream(JetstreamProtocolVersion.V1);

        Assert.Throws<NotSupportedException>(() => jetstream.KindFilter = [JetStreamEventKind.Commit]);

        jetstream.KindFilter = [];

        Assert.Empty(jetstream.KindFilter);
    }

    [Theory]
    [InlineData(JetStreamEventKind.Unknown)]
    [InlineData((JetStreamEventKind)99)]
    public void AKindFilterRejectsKindsWhichCannotBeFilteredOn(JetStreamEventKind kind)
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        Assert.Throws<ArgumentException>(() => jetstream.KindFilter = [kind]);
    }

    [Fact]
    public void V2FiltersAreLimitedToWhatTheServerAccepts()
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        Nsid[] collections = [.. Enumerable.Range(0, AtProtoJetstream.MaximumV2Collections + 1).Select(i => new Nsid($"com.example.c{i}"))];

        Assert.Throws<ArgumentException>(() => jetstream.CollectionFilter = collections);

        jetstream.CollectionFilter = collections[..AtProtoJetstream.MaximumV2Collections];

        Assert.Equal(AtProtoJetstream.MaximumV2Collections, jetstream.CollectionFilter.Count);

        Assert.Throws<ArgumentException>(() => new AtProtoJetstream(
            uri: s_server,
            collections: collections));
    }

    [Fact]
    public void V1FiltersAreNotLimited()
    {
        using AtProtoJetstream jetstream = CreateJetstream(JetstreamProtocolVersion.V1);

        Nsid[] collections = [.. Enumerable.Range(0, AtProtoJetstream.MaximumV2Collections + 1).Select(i => new Nsid($"com.example.c{i}"))];

        jetstream.CollectionFilter = collections;

        Assert.Equal(collections.Length, jetstream.CollectionFilter.Count);
    }

    [Theory]
    [InlineData(JetstreamProtocolVersion.V1, false, 0u, "wss://jetstream.example.com/subscribe?wantedCollections=app.bsky.feed.post&wantedDids=did%3aplc%3ag6ylltenitt4tp27bpwalh7b&cursor=5&maxMessageSizeBytes=1024")]
    [InlineData(JetstreamProtocolVersion.V1, true, 0u, "wss://jetstream.example.com/subscribe?wantedCollections=app.bsky.feed.post&wantedDids=did%3aplc%3ag6ylltenitt4tp27bpwalh7b&cursor=5&compress=true&maxMessageSizeBytes=1024")]
    [InlineData(JetstreamProtocolVersion.V2, false, 0u, "wss://jetstream.example.com/xrpc/network.bsky.jetstream.subscribeEvents?collections=app.bsky.feed.post&dids=did%3aplc%3ag6ylltenitt4tp27bpwalh7b&kinds=commit&kinds=sync&cursor=5&maxMessageSizeBytes=1024")]
    [InlineData(JetstreamProtocolVersion.V2, true, 20260811u, "wss://jetstream.example.com/xrpc/network.bsky.jetstream.subscribeEvents?collections=app.bsky.feed.post&dids=did%3aplc%3ag6ylltenitt4tp27bpwalh7b&kinds=commit&kinds=sync&cursor=5&zstdDictionary=20260811&maxMessageSizeBytes=1024")]
    public void TheSubscriptionUriMatchesTheProtocolVersion(JetstreamProtocolVersion protocolVersion, bool useCompression, uint dictionaryId, string expected)
    {
        using AtProtoJetstream jetstream = CreateJetstream(protocolVersion, useCompression);

        JetStreamEventKind[] kinds = protocolVersion == JetstreamProtocolVersion.V2 ? [JetStreamEventKind.Commit, JetStreamEventKind.Sync] : [];

        Uri uri = jetstream.BuildSubscriptionUri(
            s_server,
            cursor: 5,
            collections: [new Nsid("app.bsky.feed.post")],
            dids: [new Did(TestDid)],
            kinds: kinds,
            dictionaryId: dictionaryId);

        Assert.Equal(expected, uri.AbsoluteUri, ignoreCase: true);
    }

    [Fact]
    public void ACollectionFilterWithKindsWhichExcludeCommitsIsRejected()
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        Assert.Throws<InvalidOperationException>(() => jetstream.BuildSubscriptionUri(
            s_server,
            cursor: null,
            collections: [new Nsid("app.bsky.feed.post")],
            dids: [],
            kinds: [JetStreamEventKind.Identity],
            dictionaryId: 0));
    }

    [Fact]
    public void ADictionaryIdIsReadFromItsHeader()
    {
        byte[] dictionary = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(dictionary, 0xEC30A437);
        BinaryPrimitives.WriteUInt32LittleEndian(dictionary.AsSpan(4), 20260811);

        Assert.Equal(20260811u, AtProtoJetstream.ParseDictionaryId(dictionary));
    }

    [Theory]
    [InlineData(new byte[] { 0x37, 0xA4, 0x30 })]
    [InlineData(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 })]
    [InlineData(new byte[] { 0x37, 0xA4, 0x30, 0xEC, 0, 0, 0, 0 })]
    public void AMalformedDictionaryIsRejected(byte[] dictionary)
    {
        Assert.Throws<InvalidDataException>(() => AtProtoJetstream.ParseDictionaryId(dictionary));
    }

    [Theory]
    [InlineData("wss://jetstream.example.com/xrpc/x?a=1", "https://jetstream.example.com/xrpc/x?a=1")]
    [InlineData("ws://localhost:1234/xrpc/x", "http://localhost:1234/xrpc/x")]
    public void AWebSocketUriIsConvertedToHttp(string uri, string expected)
    {
        Assert.Equal(new Uri(expected), AtProtoJetstream.ToHttpUri(new Uri(uri)));
    }

    [Fact]
    public void AV2TimestampCursorIsNeverMistakenForASequence()
    {
        using AtProtoJetstream v2 = CreateJetstream();
        using AtProtoJetstream v1 = CreateJetstream(JetstreamProtocolVersion.V1);

        DateTimeOffset recent = new(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(recent.ToUnixTimeMilliseconds() * 1000, v2.ToCursor(recent));
        Assert.Equal(AtProtoJetstream.TimestampCursorThreshold, v2.ToCursor(DateTimeOffset.UnixEpoch.AddSeconds(1)));
        Assert.Equal(1_000_000, v1.ToCursor(DateTimeOffset.UnixEpoch.AddSeconds(1)));
    }

    [Fact]
    public void AV2CommitIsDerived()
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        string json = Message(
            "commit",
            """
            ,"rev":"3lomhhw5ccf2j","operation":"create","collection":"app.bsky.feed.post","rkey":"3lomhhw4vkn2j","record":{"$type":"app.bsky.feed.post","text":"hi"},"cid":"bafyreihy7s2ofgu7o3cliqgt5qbybcv3w2rmrtvrp52e2uqgtnq4urhm3y","extra":1
            """);

        Assert.True(jetstream.TryDeriveV2Event(json, out AtJetstreamEvent? derivedEvent));

        AtJetstreamCommitEvent commitEvent = Assert.IsType<AtJetstreamCommitEvent>(derivedEvent);

        Assert.Equal(JetStreamEventKind.Commit, commitEvent.Kind);
        Assert.Equal(TestDid, commitEvent.Did.ToString());
        Assert.Equal(42, commitEvent.Sequence);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 0, 19, 35, 500, TimeSpan.Zero), commitEvent.WitnessedAt);
        Assert.Equal((DateTimeOffset.Parse("2026-09-26T00:19:35.411026Z", CultureInfo.InvariantCulture).UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / TimeSpan.TicksPerMicrosecond, commitEvent.TimeStamp);
        Assert.Equal(JetstreamCommitOperation.Create, commitEvent.Commit.Operation);
        Assert.Equal("app.bsky.feed.post", commitEvent.Commit.Collection.ToString());
        Assert.Equal("3lomhhw4vkn2j", commitEvent.Commit.RKey.ToString());
        Assert.NotNull(commitEvent.Commit.Record);
        Assert.NotNull(commitEvent.Commit.Cid);
        Assert.NotNull(commitEvent.ExtensionData);
        Assert.True(commitEvent.ExtensionData.ContainsKey("extra"));
        Assert.False(commitEvent.ExtensionData.ContainsKey("$type"));
        Assert.Equal(42, jetstream.LastSequence);
    }

    [Fact]
    public void AV2IdentityIsDerived()
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        string json = Message("identity", $$""","identity":{"did":"{{TestDid}}","handle":"example.com","seq":7,"time":"2026-09-26T00:19:35Z"}""");

        Assert.True(jetstream.TryDeriveV2Event(json, out AtJetstreamEvent? derivedEvent));

        AtJetstreamIdentityEvent identityEvent = Assert.IsType<AtJetstreamIdentityEvent>(derivedEvent);

        Assert.Equal("example.com", identityEvent.Identity.Handle?.ToString());
        Assert.Equal(42, identityEvent.Sequence);
    }

    [Fact]
    public void AV2AccountIsDerived()
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        string json = Message("account", $$""","account":{"did":"{{TestDid}}","active":false,"status":"deactivated","seq":7,"time":"2026-09-26T00:19:35Z"}""");

        Assert.True(jetstream.TryDeriveV2Event(json, out AtJetstreamEvent? derivedEvent));

        AtJetstreamAccountEvent accountEvent = Assert.IsType<AtJetstreamAccountEvent>(derivedEvent);

        Assert.False(accountEvent.Account.Active);
    }

    [Fact]
    public void AV2SyncIsDerived()
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        string json = Message("sync", $$""","sync":{"did":"{{TestDid}}","rev":"3lomhhw5ccf2j","blocks":{"$bytes":"AQID"},"seq":7,"time":"2026-09-26T00:19:35Z"}""");

        Assert.True(jetstream.TryDeriveV2Event(json, out AtJetstreamEvent? derivedEvent));

        AtJetstreamSyncEvent syncEvent = Assert.IsType<AtJetstreamSyncEvent>(derivedEvent);

        Assert.Equal(JetStreamEventKind.Sync, syncEvent.Kind);
        Assert.Equal(TestDid, syncEvent.Sync.Did.ToString());
        Assert.Equal("3lomhhw5ccf2j", syncEvent.Sync.Rev);
        Assert.Equal(7, syncEvent.Sync.Sequence);
        Assert.NotNull(syncEvent.Sync.Blocks);
    }

    [Theory]
    [InlineData("commit")]
    [InlineData("identity")]
    [InlineData("account")]
    [InlineData("sync")]
    public void AV2EventMissingItsPayloadDoesNotDerive(string kind)
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        Assert.True(jetstream.TryDeriveV2Event(Message(kind, string.Empty), out AtJetstreamEvent? derivedEvent));
        Assert.Null(derivedEvent);
    }

    [Fact]
    public void AnUnknownV2KindIsRaisedAsUnknown()
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        Assert.True(jetstream.TryDeriveV2Event(Message("labels", ""","labels":[]"""), out AtJetstreamEvent? derivedEvent));

        Assert.NotNull(derivedEvent);
        Assert.Equal(typeof(AtJetstreamEvent), derivedEvent.GetType());
        Assert.Equal(JetStreamEventKind.Unknown, derivedEvent.Kind);
        Assert.Equal(42, derivedEvent.Sequence);
        Assert.NotNull(derivedEvent.ExtensionData);
        Assert.Equal(TypePrefix + "labels", derivedEvent.ExtensionData["$type"].GetString());
        Assert.True(derivedEvent.ExtensionData.ContainsKey("labels"));
    }

    [Fact]
    public void AnInfoMessageIsRaisedAsInfo()
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        InfoReceivedEventArgs? received = null;
        jetstream.InfoReceived += (sender, e) => received = e;

        string json = $$$"""{"$type":"message","payload":{"$type":"{{{TypePrefix}}}info","name":"OutdatedCursor","message":"cursor clamped"}}""";

        Assert.False(jetstream.TryDeriveV2Event(json, out AtJetstreamEvent? derivedEvent));

        Assert.Null(derivedEvent);
        Assert.NotNull(received);
        Assert.Equal("OutdatedCursor", received.Name);
        Assert.Equal("cursor clamped", received.Message);
        Assert.Null(jetstream.LastSequence);
    }

    [Fact]
    public void AnErrorFrameIsRaisedAsAFault()
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        FaultRaisedEventArgs? received = null;
        jetstream.FaultRaised += (sender, e) => received = e;

        Assert.False(jetstream.TryDeriveV2Event("""{"$type":"error","error":"ConsumerTooSlow","message":"too slow"}""", out AtJetstreamEvent? derivedEvent));

        Assert.Null(derivedEvent);
        Assert.NotNull(received);
        Assert.Equal("ConsumerTooSlow", received.Error);
        Assert.Equal("ConsumerTooSlow: too slow", received.Fault);
    }

    [Theory]
    [InlineData("""{"$type":"other"}""")]
    [InlineData("""{"$type":"message"}""")]
    [InlineData("""{"$type":"message","payload":1}""")]
    [InlineData("""{"did":"did:plc:g6ylltenitt4tp27bpwalh7b","time_us":1,"kind":"identity"}""")]
    public void AFrameWhichIsNotAV2EventDoesNotDerive(string json)
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        Assert.True(jetstream.TryDeriveV2Event(json, out AtJetstreamEvent? derivedEvent));
        Assert.Null(derivedEvent);
    }

    [Fact]
    public void LastSequenceOnlyMovesForwards()
    {
        using AtProtoJetstream jetstream = CreateJetstream();

        Assert.Null(jetstream.LastSequence);

        jetstream.TryDeriveV2Event(Message("labels", string.Empty, sequence: 10), out _);
        jetstream.TryDeriveV2Event(Message("labels", string.Empty, sequence: 5), out _);

        Assert.Equal(10, jetstream.LastSequence);
    }
}
