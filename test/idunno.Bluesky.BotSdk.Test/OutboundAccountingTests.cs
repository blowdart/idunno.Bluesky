// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text;

using idunno.Bluesky.BotSdk.DataProviders;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Checks admission-time accounting for custom repository mutations.</summary>
public sealed class OutboundAccountingTests
{
    /// <summary>Reserves write points before sending while leaving the outgoing content intact.</summary>
    /// <param name="operation">The repository endpoint.</param>
    /// <param name="points">The expected reserved cost.</param>
    [Theory]
    [InlineData("createRecord", 3)]
    [InlineData("putRecord", 3)]
    [InlineData("deleteRecord", 1)]
    [InlineData("getRecord", 0)]
    [InlineData("applyWrites", 6)]
    public async Task ReservesCompleteWriteCostAndPreservesRequestBody(string operation, int points)
    {
        BotOptions options = TestRecords.Options();
        options.DeveloperMode = false;
        options.HourlyWritePoints = 12;
        options.DailyWritePoints = 12;
        FakeTimeProvider time = new(TestRecords.Start);
        InMemoryDatabaseProvider database = new(options, time, NullLogger<InMemoryDatabaseProvider>.Instance);
        await database.InitializeAsync(CancellationToken.None);
        await database.AcquireOrRenewAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(40));
        await database.LoadStateAsync(CancellationToken.None);
        const string body = """
            {"writes":[
                {"$type":"com.atproto.repo.applyWrites#create"},
                {"$type":"com.atproto.repo.applyWrites#update"},
                {"$type":"com.atproto.repo.applyWrites#delete"}
            ]}
            """;
        using OutboundHandler handler = new(database, options, time, new WorkerHealth(time), NullLogger<OutboundHandler>.Instance)
        {
            InnerHandler = new ConfirmingHandler(body, async () =>
            {
                if (points > 0)
                {
                    Assert.True(await database.ReserveRequestAsync(13 - points, false, CancellationToken.None) >
                        time.GetUtcNow().AddMinutes(50));
                }

                time.Advance(TimeSpan.FromSeconds(5));
            })
        };
        using HttpClient client = new(handler);
        using HttpRequestMessage request = new(HttpMethod.Post, $"https://pds.example.test/xrpc/com.atproto.repo.{operation}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await database.ReserveRequestAsync(12 - points, false, CancellationToken.None));
        time.Advance(TimeSpan.FromSeconds(5));
        Assert.True(await database.ReserveRequestAsync(1, false, CancellationToken.None) > time.GetUtcNow().AddMinutes(50));
    }

    /// <summary>Refuses unaccountable batches without contacting the PDS.</summary>
    [Fact]
    public async Task UnknownBatchOperationIsRejectedBeforeSending()
    {
        BotOptions options = TestRecords.Options();
        options.DeveloperMode = false;
        FakeTimeProvider time = new(TestRecords.Start);
        InMemoryDatabaseProvider database = new(options, time, NullLogger<InMemoryDatabaseProvider>.Instance);
        using OutboundHandler handler = new(database, options, time, new WorkerHealth(time), NullLogger<OutboundHandler>.Instance)
        {
            InnerHandler = new ConfirmingHandler("", () =>
            {
                Assert.Fail("Unknown batch must not reach the network.");
                return Task.CompletedTask;
            })
        };
        using HttpClient client = new(handler);
        using StringContent content = new("""{"writes":[{"$type":"unknown"}]}""", Encoding.UTF8, "application/json");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.PostAsync("https://pds.example.test/xrpc/com.atproto.repo.applyWrites", content, TestContext.Current.CancellationToken));
    }

    private sealed class ConfirmingHandler(string expectedBody, Func<Task> sent) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(expectedBody, await request.Content!.ReadAsStringAsync(cancellationToken));
            await sent();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
