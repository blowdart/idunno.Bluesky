// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto.Authentication.Models;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class SessionDeserializationTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly Did _did = new("did:plc:ec72yg6n2sydzjvtovvdlxrk");

        [Fact]
        public void CreateSessionDeserializes()
        {
            string samplesDirectory = "sample";
            string responsesDirectory = Path.Combine(samplesDirectory, "responses");
            string serverDirectory = Path.Combine(responsesDirectory, "server");
            string captureFilePath = Path.Combine(serverDirectory, "createSession.json");
            string captureContents = File.ReadAllText(captureFilePath);

            CreateSessionResponse? sessionResponse = JsonSerializer.Deserialize<CreateSessionResponse>(captureContents, _jsonSerializerOptions);

            Assert.NotNull(sessionResponse);

            Assert.Equal("accessJwt", sessionResponse.AccessJwt);
            Assert.Equal("refreshJwt", sessionResponse.RefreshJwt);
            Assert.Equal("handle.example.org", sessionResponse.Handle);
            Assert.True(sessionResponse.Active);
            Assert.Equal(_did, sessionResponse.Did);
            Assert.Equal("email@example.org", sessionResponse.Email);
            Assert.False(sessionResponse.EmailAuthFactor);
            Assert.True(sessionResponse.EmailConfirmed);
            Assert.Null(sessionResponse.Status);
            Assert.True(sessionResponse.Active);

            Assert.NotNull(sessionResponse.DidDoc);
            Assert.NotNull(sessionResponse.DidDoc.AlsoKnownAs);
            Assert.Single(sessionResponse.DidDoc.AlsoKnownAs);
            Assert.Equal("at://handle.example.org", sessionResponse.DidDoc.AlsoKnownAs[0]);
            Assert.NotNull(sessionResponse.DidDoc.Services);
            Assert.Single(sessionResponse.DidDoc.Services);
            Assert.Equal("#atproto_pds", sessionResponse.DidDoc.Services[0].Id);
            Assert.Equal("AtprotoPersonalDataServer", sessionResponse.DidDoc.Services[0].Type);
            Assert.Equal(new Uri("https://chaga.us-west.host.bsky.network"), sessionResponse.DidDoc.Services[0].ServiceEndpoint);
        }

        [Fact]
        public void RefreshSessionDeserializes()
        {
            string samplesDirectory = "sample";
            string responsesDirectory = Path.Combine(samplesDirectory, "responses");
            string serverDirectory = Path.Combine(responsesDirectory, "server");
            string captureFilePath = Path.Combine(serverDirectory, "refreshSession.json");
            string captureContents = File.ReadAllText(captureFilePath);

            RefreshSessionResponse? sessionResponse = JsonSerializer.Deserialize<RefreshSessionResponse>(captureContents, _jsonSerializerOptions);

            Assert.NotNull(sessionResponse);

            Assert.Equal("accessJwt", sessionResponse.AccessJwt);
            Assert.Equal("refreshJwt", sessionResponse.RefreshJwt);
            Assert.Equal("handle.example.org", sessionResponse.Handle);
            Assert.True(sessionResponse.Active);
            Assert.Equal(_did, sessionResponse.Did);
            Assert.Null(sessionResponse.Status);
            Assert.True(sessionResponse.Active);

            Assert.NotNull(sessionResponse.DidDoc);
            Assert.NotNull(sessionResponse.DidDoc.AlsoKnownAs);
            Assert.Single(sessionResponse.DidDoc.AlsoKnownAs);
            Assert.Equal("at://handle.example.org", sessionResponse.DidDoc.AlsoKnownAs[0]);
            Assert.NotNull(sessionResponse.DidDoc.Services);
            Assert.Single(sessionResponse.DidDoc.Services);
            Assert.Equal("#atproto_pds", sessionResponse.DidDoc.Services[0].Id);
            Assert.Equal("AtprotoPersonalDataServer", sessionResponse.DidDoc.Services[0].Type);
            Assert.Equal(new Uri("https://chaga.us-west.host.bsky.network"), sessionResponse.DidDoc.Services[0].ServiceEndpoint);
        }

        [Fact]
        public void GetSessionDeserializes()
        {
            string samplesDirectory = "sample";
            string responsesDirectory = Path.Combine(samplesDirectory, "responses");
            string serverDirectory = Path.Combine(responsesDirectory, "server");
            string captureFilePath = Path.Combine(serverDirectory, "getSession.json");
            string captureContents = File.ReadAllText(captureFilePath);

            GetSessionResponse? sessionResponse = JsonSerializer.Deserialize<GetSessionResponse>(captureContents, _jsonSerializerOptions);

            Assert.NotNull(sessionResponse);

            Assert.Equal("handle.example.org", sessionResponse.Handle);
            Assert.True(sessionResponse.Active);
            Assert.Equal(_did, sessionResponse.Did);
            Assert.Null(sessionResponse.Status);
            Assert.True(sessionResponse.Active);

            Assert.NotNull(sessionResponse.DidDoc);
            Assert.NotNull(sessionResponse.DidDoc.AlsoKnownAs);
            Assert.Single(sessionResponse.DidDoc.AlsoKnownAs);
            Assert.Equal("at://handle.example.org", sessionResponse.DidDoc.AlsoKnownAs[0]);
            Assert.NotNull(sessionResponse.DidDoc.Services);
            Assert.Single(sessionResponse.DidDoc.Services);
            Assert.Equal("#atproto_pds", sessionResponse.DidDoc.Services[0].Id);
            Assert.Equal("AtprotoPersonalDataServer", sessionResponse.DidDoc.Services[0].Type);
            Assert.Equal(new Uri("https://chaga.us-west.host.bsky.network"), sessionResponse.DidDoc.Services[0].ServiceEndpoint);
        }
    }
}
