// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

/// <summary>Exercises lexicon limits, cursor binding and output bounds through actual HTTP routing.</summary>
public class QueryBoundaryTests
{
    [Fact]
    public async Task DefaultLimitAndMaximumLimitDoNotChangeSnapshotOrFilterBinding()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(initialize: async store =>
        {
            SignedLabelSnapshot[] labels = Enumerable.Range(0, 260).Select(index => Signed(key, $"did:plc:subject{index}")).ToArray();
            await store.AppendAsync("boundary", labels, cancellationToken: token);
        });
        string filters = "uriPatterns=did:plc:subject*&sources=did:plc:other&sources=did:plc:label-issuer";
        using HttpResponseMessage first = await fixture.Client.GetAsync("/xrpc/com.atproto.label.queryLabels?" + filters, token);
        using JsonDocument page = JsonDocument.Parse(await first.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(50, page.RootElement.GetProperty("labels").GetArrayLength());
        string cursor = page.RootElement.GetProperty("cursor").GetString()!;
        // Set semantics allow duplicates and different filter order, but never a changed filter.
        using HttpResponseMessage second = await fixture.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?uriPatterns=did:plc:subject*&uriPatterns=did:plc:subject*" +
            "&sources=did:plc:label-issuer&sources=did:plc:other&limit=250&cursor=" + Uri.EscapeDataString(cursor), token);
        using JsonDocument remainder = JsonDocument.Parse(await second.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(210, remainder.RootElement.GetProperty("labels").GetArrayLength());
        Assert.False(remainder.RootElement.TryGetProperty("cursor", out _));
        await fixture.PruneAsync(60);
        using HttpResponseMessage expired = await fixture.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?" + filters + "&cursor=" + Uri.EscapeDataString(cursor), token);
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        await using TestServerBuilder other = await TestServerBuilder.CreateAsync();
        using HttpResponseMessage foreign = await other.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?" + filters + "&cursor=" + Uri.EscapeDataString(cursor), token);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputLimitsFailExplicitlyInsteadOfReturningPartialSuccess(bool subscription)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            options: new LabelerHostingOptions { MaximumMessageBytes = 1024 },
            initialize: async store =>
            {
                await store.AppendAsync(
                    "oversized",
                    [Signed(key, "did:example:" + new string('a', 2000))],
                    cancellationToken: token);
            });
        if (subscription)
        {
            using System.Net.WebSockets.WebSocket socket = await fixture.ConnectAsync("?cursor=0");
            byte[] buffer = new byte[1024];
            using CancellationTokenSource watchdog = new(TimeSpan.FromSeconds(30));
            System.Net.WebSockets.ValueWebSocketReceiveResult received = await socket.ReceiveAsync(buffer.AsMemory(), watchdog.Token);
            Assert.True(received.EndOfMessage);
            System.Formats.Cbor.CborReader reader = new(buffer.AsMemory(0, received.Count), allowMultipleRootLevelValues: true);
            reader.SkipValue();
            reader.ReadStartMap();
            Assert.Equal("error", reader.ReadTextString());
            Assert.Equal("ServiceUnavailable", reader.ReadTextString());
        }
        else
        {
            using HttpResponseMessage response = await fixture.Client.GetAsync("/xrpc/com.atproto.label.queryLabels?uriPatterns=*", token);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("ServiceUnavailable", await response.Content.ReadAsStringAsync(token));
        }
    }

    [Fact]
    public void RegistrationRejectsMissingDuplicateAndUnboundedConfiguration()
    {
        ServiceCollection missing = new();
        Assert.Throws<InvalidOperationException>(() => missing.AddBlueskyLabeler(TestServerBuilder.s_issuer));
        ServiceCollection services = new();
        services.AddSqliteLabelStorage("Data Source=not-opened-by-registration.db");
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddBlueskyLabeler(
            TestServerBuilder.s_issuer, new LabelerHostingOptions { QueryTimeout = Timeout.InfiniteTimeSpan }));
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddBlueskyLabeler(
            TestServerBuilder.s_issuer, new LabelerHostingOptions { MaximumSubscriptions = 0 }));
        services.AddBlueskyLabeler(TestServerBuilder.s_issuer);
        Assert.Throws<InvalidOperationException>(() => services.AddBlueskyLabeler(TestServerBuilder.s_issuer));
    }

    /// <summary>Creates immutable signed input without using the clock for any deadline or scheduling assertion.</summary>
    private static SignedLabelSnapshot Signed(LabelSigningKey key, string uri) => new(key.CreateLabel(
        source: TestServerBuilder.s_issuer,
        uri: uri,
        value: "spam",
        creationTimestamp: DateTimeOffset.UnixEpoch));
}
