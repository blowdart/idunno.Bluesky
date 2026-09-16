// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat;
using idunno.Bluesky.RichText;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class SendMessageValidationTests
{
    private static readonly Did s_did = "did:plc:test";

    private static AccessCredentials CreateCredentials()
    {
        return new AccessCredentials(
            service: TestServerBuilder.DefaultUri,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
            refreshToken: "refreshToken");
    }

    [Fact]
    public async Task SendMessageRejectsAnOverLongMessageWithoutExtractingFacets()
    {
        RecordingFacetExtractor facetExtractor = new();

        using BlueskyAgent agent = new(new BlueskyAgentOptions { FacetExtractor = facetExtractor })
        {
            Credentials = CreateCredentials(),
            Service = TestServerBuilder.DefaultUri
        };

        // Long enough that extraction would scan it, and every mention in it would be resolved over the
        // network, before MessageInput rejected the message for being too long.
        string message = new('a', Maximum.MessageLengthInBytes + 1);

        ArgumentOutOfRangeException exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await agent.SendMessage(
                "conversationId",
                message,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("message", exception.ParamName);
        Assert.Equal(0, facetExtractor.CallCount);
    }

    [Fact]
    public async Task SendMessageStillExtractsFacetsFromAMessageWithinTheLengthLimit()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        RecordingFacetExtractor facetExtractor = new();

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/xrpc/chat.bsky.convo.sendMessage")
            {
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync(
                    """{"id":"messageId","rev":"rev","text":"hello","sender":{"did":"did:plc:test"},"sentAt":"2024-01-01T00:00:00.000Z"}""");
                return;
            }

            response.StatusCode = 404;
        });

        using BlueskyAgent agent = new(
            new TestHttpClientFactory(testServer),
            new BlueskyAgentOptions { FacetExtractor = facetExtractor })
        {
            Credentials = CreateCredentials(),
            Service = TestServerBuilder.DefaultUri
        };

        AtProtoHttpResult<MessageView> result = await agent.SendMessage(
            "conversationId",
            "hello",
            cancellationToken: testToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, facetExtractor.CallCount);
    }

    private sealed class RecordingFacetExtractor : IFacetExtractor
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task<IList<Facet>> ExtractFacets(string text, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);

            return Task.FromResult<IList<Facet>>([]);
        }
    }
}
