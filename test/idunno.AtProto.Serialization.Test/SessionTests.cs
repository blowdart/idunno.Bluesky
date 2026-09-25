// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json;

using idunno.AtProto.Authentication;
using idunno.AtProto.Authentication.Models;

using Microsoft.IdentityModel.Tokens;

namespace idunno.AtProto.Serialization.Test;

[ExcludeFromCodeCoverage]
public class SessionTests
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    private readonly Handle _handle = new("handle.example.org");

    private readonly Did _did = new("did:plc:ec72yg6n2sydzjvtovvdlxrk");

    private static string CreateJwt()
    {
        static string Encode(string value) =>
            Base64UrlEncoder.Encode(System.Text.Encoding.UTF8.GetBytes(value));

        long expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

        return $"{Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}")}." +
               $"{Encode($"{{\"sub\":\"did:plc:ec72yg6n2sydzjvtovvdlxrk\",\"exp\":{expires}}}")}.";
    }

    public SessionTests()
    {
        _jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _jsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
    }

    [Theory]
    [InlineData("takendown", AccountStatus.Takendown)]    [InlineData("suspended", AccountStatus.Suspended)]
    [InlineData("deactivated", AccountStatus.Deactivated)]
    [InlineData("deleted", AccountStatus.Deleted)]
    [InlineData("throttled", AccountStatus.Throttled)]
    [InlineData("TAKENDOWN", AccountStatus.Takendown)]
    [InlineData("invented-upstream", AccountStatus.Unknown)]
    public void EveryKnownAccountStatusSurvivesBeingTurnedIntoASession(string status, AccountStatus expected)
    {
        CreateSessionResponse createSessionResponse = new(
            accessJwt: "accessJwt",
            refreshJwt: "refreshJwt",
            handle: _handle,
            did: _did,
            active: false,
            status: status);

        GetSessionResponse getSessionResponse = new(
            handle: _handle,
            did: _did,
            email: null,
            emailConfirmed: null,
            emailAuthFactor: null,
            didDoc: null,
            active: false,
            status: status);

        AccessCredentials accessCredentials = new(
            new Uri("https://pds.example.org"),
            AuthenticationType.UsernamePassword,
            CreateJwt(),
            "refreshJwt");

        Assert.Equal(expected, new Session(createSessionResponse).Status);
        Assert.Equal(expected, new Session(getSessionResponse, accessCredentials).Status);
    }

    [Fact]
    public void ASessionWithNoAccountStatusHasNoStatus()
    {
        CreateSessionResponse createSessionResponse = new(
            accessJwt: "accessJwt",
            refreshJwt: "refreshJwt",
            handle: _handle,
            did: _did);

        Assert.Null(new Session(createSessionResponse).Status);
    }

    [Fact]
    public void CreateSessionRequestIdentifierPasswordRequestSerializesProperly()
    {
        const string identifier = "identifier";
        const string password = "password";

        CreateSessionRequest request = new()
        {
            Identifier = identifier,
            Password = password
        };

        string requestAsJson = JsonSerializer.Serialize(request, _jsonSerializerOptions);

        Assert.NotNull(requestAsJson);
        Assert.Equal("{\"identifier\":\"identifier\",\"password\":\"password\"}", requestAsJson);
    }

    [Fact]
    public void CreateSessionRequestIdentifierPasswordAuthFactorRequestSerializesProperly()
    {
        const string identifier = "identifier";
        const string password = "password";
        const string authFactorToken = "authFactorToken";

        CreateSessionRequest request = new()
        {
            Identifier = identifier,
            Password = password,
            AuthFactorToken = authFactorToken
        };

        string requestAsJson = JsonSerializer.Serialize(request, _jsonSerializerOptions);

        Assert.NotNull(requestAsJson);
        Assert.Equal("{\"identifier\":\"identifier\",\"password\":\"password\",\"authFactorToken\":\"authFactorToken\"}", requestAsJson);
    }

    [Fact]
    public void CreateSessionRequestRoundTrips()
    {
        const string identifier = "identifier";
        const string password = "password";
        const string authFactorToken = "authFactorToken";

        CreateSessionRequest request = new()
        {
            Identifier = identifier,
            Password = password,
            AuthFactorToken = authFactorToken
        };

        string requestAsJson = JsonSerializer.Serialize(request, _jsonSerializerOptions);

        CreateSessionRequest? deserializedRequest = JsonSerializer.Deserialize<CreateSessionRequest>(requestAsJson, _jsonSerializerOptions);

        Assert.NotNull(deserializedRequest);

        Assert.Equal(identifier, deserializedRequest.Identifier);
        Assert.Equal(password, deserializedRequest.Password);
        Assert.Equal(authFactorToken, deserializedRequest.AuthFactorToken);
    }
}