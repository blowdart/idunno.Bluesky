using System.Net;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.AtProto.Server;
using idunno.Bluesky;
using idunno.Bluesky.Actor;

Uri service = new("https://offline.invalid");
using OfflineHttpMessageHandler handler = new();
OfflineHttpClientFactory httpClientFactory = new(handler);

using (AtProtoAgent agent = new(service, httpClientFactory))
{
    AtProtoHttpResult<ServerDescription> describeServerResult = await agent.DescribeServer(service);
    describeServerResult.EnsureSucceeded();

    if (describeServerResult.Result.Did != "did:web:offline.invalid" ||
        describeServerResult.Result.Contact?.Email != "test@offline.invalid")
    {
        throw new InvalidOperationException("The server description was not deserialized correctly.");
    }

    AtProtoHttpResult<AtProtoRepositoryRecord> getRecordResult = await agent.GetRawRecord(
        new AtUri("at://did:plc:test/app.bsky.actor.profile/self"),
        service,
        CancellationToken.None);
    getRecordResult.EnsureSucceeded();

    if (getRecordResult.Result.Value?["displayName"]?.GetValue<string>() != "Offline Profile")
    {
        throw new InvalidOperationException("The repository record was not deserialized correctly.");
    }
}

using (BlueskyAgent agent = new(httpClientFactory))
{
    AtProtoHttpResult<ProfileViewDetailed> getProfileResult = await agent.GetProfile(
        AtIdentifier.Create("offline.invalid"));
    getProfileResult.EnsureSucceeded();

    if (getProfileResult.Result.Did != "did:plc:test" ||
        getProfileResult.Result.Handle != "offline.invalid" ||
        getProfileResult.Result.DisplayName != "Offline User")
    {
        throw new InvalidOperationException("The Bluesky profile was not deserialized correctly.");
    }
}

if (handler.RequestCount != 3)
{
    throw new InvalidOperationException($"Expected three offline requests, but received {handler.RequestCount}.");
}

Console.WriteLine("Offline trimming and Native AOT smoke test passed.");

internal sealed class OfflineHttpClientFactory(OfflineHttpMessageHandler handler) : IHttpClientFactory
{
    private readonly OfflineHttpMessageHandler _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}

internal sealed class OfflineHttpMessageHandler : HttpMessageHandler
{
    private const string DescribeServerResponse = """
        {
          "did": "did:web:offline.invalid",
          "availableUserDomains": ["offline.invalid"],
          "inviteCodeRequired": false,
          "phoneVerificationRequired": false,
          "links": {
            "privacyPolicy": "https://offline.invalid/privacy",
            "termsOfService": "https://offline.invalid/terms"
          },
          "contact": {
            "email": "test@offline.invalid"
          },
          "blobUploadLimit": 10000000
        }
        """;

    private const string GetRecordResponse = """
        {
          "uri": "at://did:plc:test/app.bsky.actor.profile/self",
          "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
          "value": {
            "$type": "app.bsky.actor.profile",
            "displayName": "Offline Profile",
            "description": "An offline repository record."
          }
        }
        """;

    private const string GetProfileResponse = """
        {
          "did": "did:plc:test",
          "handle": "offline.invalid",
          "displayName": "Offline User",
          "description": "An offline profile.",
          "followersCount": 1,
          "followsCount": 2,
          "postsCount": 3,
          "labels": []
        }
        """;

    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Headers.Authorization is not null)
        {
            throw new InvalidOperationException("The public API smoke test sent authentication credentials.");
        }

        RequestCount++;

        string response = request.RequestUri?.AbsolutePath switch
        {
            "/xrpc/com.atproto.server.describeServer" => DescribeServerResponse,
            "/xrpc/com.atproto.repo.getRecord" => GetRecordResponse,
            "/xrpc/app.bsky.actor.getProfile" => GetProfileResponse,
            _ => throw new InvalidOperationException($"Unexpected request URI {request.RequestUri}.")
        };

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json"),
            RequestMessage = request
        });
    }
}
