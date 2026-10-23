// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Firehose;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

/// <summary>Exercises mapped XRPC endpoints and independent wire decoding rather than testing only internal helpers.</summary>
public class HostingTests
{
    private static readonly DateTimeOffset s_instant = new(
        year: 2026,
        month: 1,
        day: 2,
        hour: 3,
        minute: 4,
        second: 5,
        offset: TimeSpan.Zero);

    [Theory]
    [InlineData("", 400)]
    [InlineData("?uriPatterns=*", 200)]
    [InlineData("?uriPatterns=did:plc:subject&limit=0", 400)]
    [InlineData("?uriPatterns=*&limit=251", 400)]
    [InlineData("?uriPatterns=*&limit=1&limit=2", 400)]
    [InlineData("?uriPatterns=*&sources=not-a-did", 400)]
    [InlineData("?uriPatterns=a*b", 400)]
    [InlineData("?uriPatterns=*&cursor=garbage", 400)]
    [InlineData("?uriPatterns=*&sources=did:plc:other", 200)]
    public async Task ValidatesQueryInputs(string query, int status)
    {
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        using HttpResponseMessage response = await fixture.Client.GetAsync("/xrpc/com.atproto.label.queryLabels" + query, TestContext.Current.CancellationToken);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task ReturnsStandardMethodAndUpgradeErrors()
    {
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        using HttpResponseMessage method = await fixture.Client.PostAsync("/xrpc/com.atproto.label.queryLabels", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, method.StatusCode);
        Assert.Contains("GET", method.Content.Headers.Allow);
        using HttpResponseMessage upgrade = await fixture.Client.GetAsync("/xrpc/com.atproto.label.subscribeLabels", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UpgradeRequired, upgrade.StatusCode);
        Assert.Contains("websocket", upgrade.Headers.Upgrade.Select(value => value.Name));
    }

    [Theory]
    [InlineData(LabelSigningCurve.P256)]
    [InlineData(LabelSigningCurve.Secp256k1)]
    public async Task ServesReferenceRetainedContentAndStablePaginationThroughSdk(LabelSigningCurve curve)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(curve);
        SignedLabelSnapshot[] labels = Shapes(key);
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(initialize: async store =>
        {
            await store.AppendAsync("initial", labels[..3]);
            await store.PublishAsync();
        });
        AtProtoHttpResult<PagedReadOnlyCollection<Label>> first = await QueryAsync(
            fixture: fixture,
            patterns: ["*"],
            sources: null,
            limit: 1,
            cursor: null);
        Assert.True(first.Succeeded);
        Assert.Equal(labels[1].GetCanonicalUnsignedBytes(), Assert.Single(first.Result).GetCanonicalUnsignedBytes());
        string cursor = first.Result.Cursor!;
        await fixture.Store.AppendAsync("new", [labels[3]], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<PagedReadOnlyCollection<Label>> second = await QueryAsync(
            fixture: fixture,
            patterns: ["*"],
            sources: null,
            limit: 250,
            cursor: cursor);
        Assert.True(second.Succeeded);
        Assert.Equal(labels[2].GetCanonicalUnsignedBytes(), Assert.Single(second.Result).GetCanonicalUnsignedBytes());
        Assert.Null(second.Result.Cursor);
        AtProtoHttpResult<PagedReadOnlyCollection<Label>> current = await QueryAsync(
            fixture: fixture,
            patterns: ["*"],
            sources: null,
            limit: 250,
            cursor: null);
        Assert.True(current.Succeeded);
        Assert.Equal(2, current.Result.Count);
        Assert.Contains(current.Result, label => label.IsNegationLabel);
        foreach (Label label in first.Result.Concat(second.Result).Concat(current.Result))
        {
            label.Verify(Document(key));
        }

        AtProtoHttpResult<PagedReadOnlyCollection<Label>> changedFilter = await QueryAsync(
            fixture: fixture,
            patterns: ["did:plc:subject"],
            sources: null,
            limit: 1,
            cursor: cursor);
        Assert.Equal(HttpStatusCode.BadRequest, changedFilter.StatusCode);
        AtProtoHttpResult<PagedReadOnlyCollection<Label>> excluded = await QueryAsync(
            fixture: fixture,
            patterns: ["*"],
            sources: [new Did("did:plc:other")],
            limit: 250,
            cursor: null);
        Assert.True(excluded.Succeeded);
        Assert.Empty(excluded.Result);
    }

    [Fact]
    public async Task AcceptsHttp2ExtendedConnectHandshake()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot label = Shapes(key)[0];
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            network: true,
            initialize: async store =>
            {
                await store.AppendAsync("http2", [label]);
                await store.PublishAsync();
            },
            http2: true);
        using HttpMessageInvoker invoker = new(new SocketsHttpHandler());
        using ClientWebSocket socket = new();
        socket.Options.HttpVersion = HttpVersion.Version20;
        socket.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        using CancellationTokenSource watchdog = new(TimeSpan.FromSeconds(30));
        await socket.ConnectAsync(new Uri(fixture.SocketAddress + "?cursor=0"), invoker, watchdog.Token);

        CborReader frame = new(await ReceiveAsync(socket), CborConformanceMode.Canonical, allowMultipleRootLevelValues: true);
        Assert.Equal(2, frame.ReadStartMap());
        Assert.Equal("t", frame.ReadTextString());
        Assert.Equal("#labels", frame.ReadTextString());
        Assert.Equal("op", frame.ReadTextString());
        Assert.Equal(1, frame.ReadInt32());
        frame.ReadEndMap();
        Assert.Equal(2, frame.ReadStartMap());
        Assert.Equal("seq", frame.ReadTextString());
        Assert.Equal(1, frame.ReadInt64());
        socket.Abort();
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("CONNECT")]
    public async Task RejectsNonWebSocketSubscriptionMethods(string method)
    {
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        using HttpRequestMessage request = new(new HttpMethod(method), "/xrpc/com.atproto.label.subscribeLabels");
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Contains("GET", response.Content.Headers.Allow);
        Assert.Contains("CONNECT", response.Content.Headers.Allow);
    }

    [Theory]
    [InlineData("?cursor=999", "FutureCursor")]
    [InlineData("?cursor=-1", "InvalidRequest")]
    [InlineData("?cursor=9007199254740992", "InvalidRequest")]
    [InlineData("?cursor=abc", "InvalidRequest")]
    [InlineData("?cursor=0&cursor=0", "InvalidRequest")]
    public async Task SendsSubscriptionErrorsInsideBinaryStream(string query, string expected)
    {
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        using WebSocket socket = await fixture.ConnectAsync(query);
        CborReader frame = new(await ReceiveAsync(socket), CborConformanceMode.Canonical, allowMultipleRootLevelValues: true);
        Assert.Equal(1, frame.ReadStartMap());
        Assert.Equal("op", frame.ReadTextString());
        Assert.Equal(-1, frame.ReadInt32());
        frame.ReadEndMap();
        Assert.Equal(2, frame.ReadStartMap());
        Assert.Equal("error", frame.ReadTextString());
        Assert.Equal(expected, frame.ReadTextString());
        Assert.Equal("message", frame.ReadTextString());
        frame.ReadTextString();
        frame.ReadEndMap();
        Assert.Equal(0, frame.BytesRemaining);
    }

    [Theory]
    [InlineData(LabelSigningCurve.P256)]
    [InlineData(LabelSigningCurve.Secp256k1)]
    public async Task BackfillAndExclusiveReconnectVerifyThroughRealSdkWebSockets(LabelSigningCurve curve)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(curve);
        SignedLabelSnapshot[] labels = Shapes(key);
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(network: true, initialize: async store =>
        {
            await store.AppendAsync("initial", labels);
            await store.PublishAsync();
        });
        using HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false });
        using AtProtoFirehose firehose = new(client, new FirehoseOptions { LabelerUri = fixture.Address });
        using CancellationTokenSource watchdog = new(TimeSpan.FromSeconds(30));
        await using IAsyncEnumerator<FirehoseEvent> events = firehose.SubscribeLabelsAsync(
            cursor: 0,
            maximumReconnectAttempts: 0,
            cancellationToken: watchdog.Token).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await events.MoveNextAsync());
        FirehoseLabelsEvent first = Assert.IsType<FirehoseLabelsEvent>(events.Current);
        Assert.Equal(3, first.Sequence);
        Assert.Single(first.Labels).Verify(Document(key));
        Assert.True(await events.MoveNextAsync());
        FirehoseLabelsEvent second = Assert.IsType<FirehoseLabelsEvent>(events.Current);
        Assert.Equal(4, second.Sequence);
        Assert.Single(second.Labels).Verify(Document(key));

        using AtProtoFirehose reconnect = new(client, new FirehoseOptions { LabelerUri = fixture.Address });
        await using IAsyncEnumerator<FirehoseEvent> resumed = reconnect.SubscribeLabelsAsync(
            cursor: first.Sequence,
            maximumReconnectAttempts: 0,
            cancellationToken: watchdog.Token).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await resumed.MoveNextAsync());
        Assert.Equal(4, Assert.IsType<FirehoseLabelsEvent>(resumed.Current).Sequence);
    }

    [Theory]
    [InlineData(LabelSigningCurve.P256, false)]
    [InlineData(LabelSigningCurve.P256, true)]
    [InlineData(LabelSigningCurve.Secp256k1, false)]
    [InlineData(LabelSigningCurve.Secp256k1, true)]
    public async Task PreservesLegacySignedTextAndExplicitFalseOnBothDeliveryPaths(LabelSigningCurve curve, bool rawSnapshot)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(curve);
        Label original = JsonSerializer.Deserialize<Label>(
            """
            {"ver":1,"src":"did:plc:label-issuer","uri":"at://did:plc:subject/app.bsky.actor.profile/self",
            "cid":"BAFYREIGH5FJR6AN3QUJVFBM6XMSO6JYHIDO2DGUIXAEQP3JTCQFT2GOFBA",
            "val":"spam","neg":false,"cts":"2026-01-02T04:04:05.123456789+01:00","exp":"2027-01-02T04:04:05.987654321+01:00"}
            """, AtProtoJsonSerializerOptions.Default)!;
        SignedLabelSnapshot signed = new(key.Sign(original));
        if (rawSnapshot)
        {
            string json = Encoding.UTF8.GetString(signed.GetJsonBytes());
            foreach (string field in new[] { "ver", "src", "uri", "cid", "val", "neg", "cts", "exp", "sig" })
            {
                json = json.Replace($"\"{field}\":", $"\"{field.ToUpperInvariant()}\":", StringComparison.Ordinal);
            }

            json = json.Replace("\"VER\":1", "\"VER\":\"1\"", StringComparison.Ordinal);
            signed = new SignedLabelSnapshot(Encoding.UTF8.GetBytes(json));
        }
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(network: true, initialize: async store =>
        {
            await store.AppendAsync("signed-shape", [signed], cancellationToken: TestContext.Current.CancellationToken);
        });
        AtProtoHttpResult<PagedReadOnlyCollection<Label>> query = await QueryAsync(
            fixture: fixture,
            patterns: ["*"],
            sources: null,
            limit: 50,
            cursor: null);
        Assert.True(query.Succeeded);
        Label jsonLabel = Assert.Single(query.Result);
        Assert.Equal(signed.GetCanonicalUnsignedBytes(), jsonLabel.GetCanonicalUnsignedBytes());
        jsonLabel.Verify(Document(key));
        using HttpClient client = new();
        using AtProtoFirehose firehose = new(client, new FirehoseOptions { LabelerUri = fixture.Address });
        using CancellationTokenSource watchdog = new(TimeSpan.FromSeconds(30));
        await using IAsyncEnumerator<FirehoseEvent> events = firehose.SubscribeLabelsAsync(
            cursor: 0,
            maximumReconnectAttempts: 0,
            cancellationToken: watchdog.Token).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await events.MoveNextAsync());
        Label streamLabel = Assert.Single(Assert.IsType<FirehoseLabelsEvent>(events.Current).Labels);
        Assert.Equal(signed.GetCanonicalUnsignedBytes(), streamLabel.GetCanonicalUnsignedBytes());
        streamLabel.Verify(Document(key));
    }

    /// <summary>Creates independent account and CID-scoped record slots with same-slot replacement and expiry.</summary>
    private static SignedLabelSnapshot[] Shapes(LabelSigningKey key) =>
    [
        new(key.CreateLabel(
            source: TestServerBuilder.Issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: s_instant)),
        new(key.CreateLabel(
            source: TestServerBuilder.Issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: s_instant.AddSeconds(1))),
        new(key.CreateLabel(
            source: TestServerBuilder.Issuer,
            uri: "at://did:plc:subject/app.bsky.actor.profile/self",
            value: "spam",
            creationTimestamp: s_instant,
            cid: new Cid("bafyreigh5fjr6an3qujvfbm6xmso6jyhido2dguixaeqp3jtcqft2gofba"),
            expiresAt: s_instant.AddSeconds(1))),
        new(key.CreateNegation(
            source: TestServerBuilder.Issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: s_instant.AddSeconds(2)))
    ];

    /// <summary>Supplies the public verification document without fetching external identities.</summary>
    private static DidDocument Document(LabelSigningKey key) => JsonSerializer.Deserialize<DidDocument>(
        $$"""
        {"id":"did:plc:label-issuer","verificationMethod":[{"id":"did:plc:label-issuer#atproto_label",
        "type":"Multikey","controller":"did:plc:label-issuer","publicKeyMultibase":"{{key.ExportPublicKeyMultibase()}}"}]}
        """, AtProtoJsonSerializerOptions.Default)!;

    /// <summary>Uses the shipped SDK request and response pipeline against the mapped query handler.</summary>
    private static Task<AtProtoHttpResult<PagedReadOnlyCollection<Label>>> QueryAsync(
        TestServerBuilder fixture,
        string[] patterns,
        Did[]? sources,
        int limit,
        string? cursor) => AtProtoServer.QueryLabels(
            uriPatterns: patterns,
            sources: sources,
            limit: limit,
            cursor: cursor,
            service: fixture.Address,
            accessCredentials: null,
            httpClient: fixture.Client);

    /// <summary>Reads a bounded complete server frame; timeout is only a hang watchdog.</summary>
    private static async Task<byte[]> ReceiveAsync(WebSocket socket)
    {
        using CancellationTokenSource watchdog = new(TimeSpan.FromSeconds(30));
        using MemoryStream buffer = new();
        byte[] bytes = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(bytes, watchdog.Token);
            Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
            buffer.Write(bytes, 0, result.Count);
            Assert.True(buffer.Length <= 1024 * 1024);
        }
        while (!result.EndOfMessage);
        return buffer.ToArray();
    }
}
