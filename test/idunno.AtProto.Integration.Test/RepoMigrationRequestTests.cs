// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class RepoMigrationRequestTests
{
    private static readonly Did s_did = "did:plc:hfgp6pj3akhqxntgqwramlbg";

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    [Fact]
    public void RepositoryEndpointSerializerPreservesEveryReachableType()
    {
        JsonSerializerOptions options = AtProtoServer.AtProtoJsonSerializerOptions;
        Assert.IsAssignableFrom<JsonSerializerContext>(TypeResolver.JsonTypeInfoResolver);
        JsonSerializerOptions contracts = new(options) { TypeInfoResolver = TypeResolver.JsonTypeInfoResolver };
        contracts.Converters.Clear();
        HashSet<Type> visited = [];
        foreach (Type root in new[] { typeof(ListMissingBlobsResponse), typeof(MissingBlob), typeof(EmptyResponse), typeof(AtErrorDetail) })
        {
            Verify(root);
        }

        void Verify(Type type)
        {
            if (!visited.Add(type))
            {
                return;
            }

            Assert.Equal(type, options.GetTypeInfo(type).Type);
            JsonTypeInfo metadata = contracts.GetTypeInfo(type);
            Assert.Equal(type, metadata.Type);
            foreach (JsonPropertyInfo property in metadata.Properties)
            {
                if (property.Get is not null || property.Set is not null)
                {
                    Verify(property.PropertyType);
                }
            }

            if (Nullable.GetUnderlyingType(type) is Type underlying)
            {
                Verify(underlying);
            }
            else if (metadata.Kind is JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary)
            {
                foreach (Type argument in type.IsArray ? [type.GetElementType()!] : type.GetGenericArguments())
                {
                    Verify(argument);
                }
            }
        }
    }

    [Fact]
    public void RepositoryEndpointSerializerDoesNotFallBackToReflection()
    {
        Assert.Throws<NotSupportedException>(() => AtProtoServer.AtProtoJsonSerializerOptions.GetTypeInfo(typeof(UnregisteredDto)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportRepoForwardsLoggingAndReportsErrors(bool useAgent)
    {
        using RecordingLoggerProvider logs = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        using TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"InvalidRepo","message":"Invalid CAR."}""", TestContext.Current.CancellationToken);
        });
        using HttpClient httpClient = testServer.CreateClient();
        using AtProtoAgent agent = new(
            TestServerBuilder.DefaultUri,
            new TestHttpClientFactory(testServer),
            options: new AtProtoAgentOptions { LoggerFactory = loggerFactory })
        {
            Credentials = CreateCredentials()
        };

        AtProtoHttpResult<EmptyResponse> result = useAgent
            ? await agent.ImportRepo([1], TestContext.Current.CancellationToken)
            : await AtProtoServer.ImportRepo(
                [1], TestServerBuilder.DefaultUri, CreateCredentials(), httpClient,
                loggerFactory: loggerFactory, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("InvalidRepo", result.AtErrorDetail?.Error);
        Assert.Contains(logs.Entries, entry => entry.Message.Contains("com.atproto.repo.importRepo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportRepoRejectsEmptyCarWithTheDocumentedException()
    {
        using HttpClient httpClient = new();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => AtProtoServer.ImportRepo(
            [], TestServerBuilder.DefaultUri, CreateCredentials(), httpClient, cancellationToken: TestContext.Current.CancellationToken));
        using AtProtoAgent agent = new(TestServerBuilder.DefaultUri);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.ImportRepo([], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListMissingBlobsSendsPaginationParametersAndReturnsBlobs()
    {
        string? queryString = null;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                queryString = context.Request.QueryString.Value;
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    """
                    {
                      "blobs": [
                        null,
                        {
                          "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
                          "recordUri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3mei4u5pu6226"
                        }
                      ],
                      "cursor": "next page"
                    }
                    """,
                    TestContext.Current.CancellationToken);
            });
        using AtProtoAgent agent = new(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer))
        {
            Credentials = CreateCredentials()
        };

        AtProtoHttpResult<PagedReadOnlyCollection<MissingBlob>> result = await agent.ListMissingBlobs(
            limit: 20,
            cursor: "next page",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("?limit=20&cursor=next%20page", queryString);
        Assert.Equal("next page", result.Result.Cursor);
        Assert.Equal(new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"), Assert.Single(result.Result).Cid);
    }

    [Fact]
    public async Task ImportRepoPostsCarWithRequiredContentTypeAndContentLength()
    {
        const string car = "CAR";
        string? contentType = null;
        long? contentLength = null;
        string? body = null;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                contentType = context.Request.ContentType;
                contentLength = context.Request.ContentLength;
                using StreamReader reader = new(context.Request.Body);
                body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{}", TestContext.Current.CancellationToken);
            });
        using AtProtoAgent agent = new(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer))
        {
            Credentials = CreateCredentials()
        };

        AtProtoHttpResult<EmptyResponse> result = await agent.ImportRepo(
            System.Text.Encoding.UTF8.GetBytes(car),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("application/vnd.ipld.car", contentType);
        Assert.Equal(car.Length, contentLength);
        Assert.Equal(car, body);
    }

    private sealed record UnregisteredDto(string Value);
}
