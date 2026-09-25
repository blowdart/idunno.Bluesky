// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto.Integration.Test;

/// <summary>
/// Covers the DNS half of handle resolution, which is answered through <see cref="TestDns"/> rather than by a real
/// name server.
/// </summary>
[ExcludeFromCodeCoverage]
public class HandleDnsResolutionTests
{
    private const string Did = "did:plc:identifier";
    private const string OtherDid = "did:plc:someoneelse";

    /// <summary>
    /// Serves <paramref name="wellKnownDid"/> from <c>/.well-known/atproto-did</c>, or 404 when it is <see langword="null"/>.
    /// </summary>
    private static TestServer CreateServer(string? wellKnownDid)
    {
        return TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpResponse response = context.Response;

            if (wellKnownDid is null)
            {
                response.StatusCode = 404;
                return;
            }

            response.StatusCode = 200;
            response.ContentType = "text/plain";
            await response.WriteAsync(wellKnownDid);
        });
    }

    private static async Task<Did?> ResolveAsync(TestServer testServer)
    {
        using HttpClient httpClient = testServer.CreateClient();

        return await AtProtoServer.ResolveHandle(
            new Handle(TestServerBuilder.DefaultDomainName),
            httpClient,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AHandleIsResolvedFromItsDidTextRecord()
    {
        using TestDns dns = TestDns.WithTextRecordsFor(TestServerBuilder.DefaultDomainName, $"did={Did}");
        using TestServer testServer = CreateServer(wellKnownDid: null);

        Assert.Equal(new Did(Did), await ResolveAsync(testServer));
    }

    [Fact]
    public async Task TheDidTextRecordIsPreferredOverTheWellKnownResponse()
    {
        using TestDns dns = TestDns.WithTextRecordsFor(TestServerBuilder.DefaultDomainName, $"did={Did}");
        using TestServer testServer = CreateServer(OtherDid);

        Assert.Equal(new Did(Did), await ResolveAsync(testServer));
    }

    [Fact]
    public async Task TextRecordsWhichAreNotDidRecordsAreIgnored()
    {
        using TestDns dns = TestDns.WithTextRecordsFor(
            TestServerBuilder.DefaultDomainName,
            "v=spf1 -all",
            "some-other-verification=abc123");
        using TestServer testServer = CreateServer(Did);

        Assert.Equal(new Did(Did), await ResolveAsync(testServer));
    }

    [Fact]
    public async Task ADidTextRecordRepeatedIdenticallyStillResolves()
    {
        using TestDns dns = TestDns.WithTextRecordsFor(TestServerBuilder.DefaultDomainName, $"did={Did}", $"did={Did}");
        using TestServer testServer = CreateServer(wellKnownDid: null);

        Assert.Equal(new Did(Did), await ResolveAsync(testServer));
    }

    [Fact]
    public async Task ConflictingDidTextRecordsMakeAHandleUnresolvable()
    {
        // The specification requires a handle carrying more than one did text record to be treated as unresolvable,
        // rather than an arbitrary record being chosen. The well known response must not be used to break the tie
        // either, as that would let the host the handle points at decide which of the conflicting records wins.
        using TestDns dns = TestDns.WithTextRecordsFor(
            TestServerBuilder.DefaultDomainName,
            $"did={Did}",
            $"did={OtherDid}");
        using TestServer testServer = CreateServer(Did);

        Assert.Null(await ResolveAsync(testServer));
    }

    [Fact]
    public async Task ConflictingDidTextRecordsAreReportedAsAnError()
    {
        RecordingLoggerProvider provider = new();

        using TestDns dns = TestDns.WithTextRecordsFor(
            TestServerBuilder.DefaultDomainName,
            $"did={Did}",
            $"did={OtherDid}");
        using TestServer testServer = CreateServer(wellKnownDid: null);
        using HttpClient httpClient = testServer.CreateClient();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(provider);
        });

        Assert.Null(await AtProtoServer.ResolveHandle(
            new Handle(TestServerBuilder.DefaultDomainName),
            httpClient,
            loggerFactory,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(
            provider.Entries,
            entry => entry.Level == LogLevel.Error && entry.EventId == 507 && entry.Message.Contains("ambiguous", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AMalformedDidTextRecordFallsBackToTheWellKnownResponse()
    {
        using TestDns dns = TestDns.WithTextRecordsFor(TestServerBuilder.DefaultDomainName, "did=not a did");
        using TestServer testServer = CreateServer(Did);

        Assert.Equal(new Did(Did), await ResolveAsync(testServer));
    }

    [Fact]
    public async Task AResolverWhichThrowsFallsBackToTheWellKnownResponse()
    {
        using TestDns dns = TestDns.WhichFails();
        using TestServer testServer = CreateServer(Did);

        Assert.Equal(new Did(Did), await ResolveAsync(testServer));
    }

    [Fact]
    public async Task AResolverWhichThrowsLeavesAHandleWithNoWellKnownResponseUnresolved()
    {
        using TestDns dns = TestDns.WhichFails();
        using TestServer testServer = CreateServer(wellKnownDid: null);

        Assert.Null(await ResolveAsync(testServer));
    }

    [Fact]
    public async Task TheDidTextRecordIsLookedUpUnderTheAtprotoSubdomain()
    {
        string? queriedHost = null;

        using TestDns dns = TestDns.WithResolver((host, _) =>
        {
            queriedHost = host;
            return Task.FromResult<IReadOnlyList<string>>([$"did={Did}"]);
        });
        using TestServer testServer = CreateServer(wellKnownDid: null);

        Assert.Equal(new Did(Did), await ResolveAsync(testServer));
        Assert.Equal($"_atproto.{TestServerBuilder.DefaultDomainName}", queriedHost);
    }
}
