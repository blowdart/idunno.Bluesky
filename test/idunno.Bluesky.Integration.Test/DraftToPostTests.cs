// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;
using idunno.AtProto.Server.Models;
using idunno.Bluesky.Drafts;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Integration.Test
{
    public class DraftToPostTests
    {
        private static readonly Did s_user = "did:plc:test";

        private static readonly string s_expectedServiceAuth = JwtBuilder.CreateJwt(
            did: null,
            issuer: s_user.ToString(),
            audience: "did:web:pds.test.internal",
            lxm: "com.atproto.repo.uploadBlob");

        private static readonly string s_expectedJobId = "jobId";

        private static readonly Guid s_expectedDeviceId = Guid.NewGuid();
        private static readonly string s_expectedDeviceName = "test harness";
        private static readonly string s_expectedDraftPostText = "Draft Post";

        private static readonly Cid s_expectedBlobCid = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        private static readonly Cid s_expectedVideoBlobCid = new("bafkreiazmokgmpqpez2l4pyd2rleluqzw6tjef7hmj76ugsk53s6gh5l2i");

        [Fact]
        public async Task SingleTextPostDraftPostsCorrectly()
        {
            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(s_user, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            AtUri expectedAtUri = new($"at://{s_user}/app.bsky.feed.post/{TimestampIdentifier.Next()}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            TimestampIdentifier expectedDraftId = TimestampIdentifier.Next();
            HttpRequest capturedRequest;
            string? capturedBody = null;

            bool deleteCalled = false;

            async Task captureCreateRequest(HttpContext context)
            {
                capturedRequest = context.Request;

                using (var reader = new StreamReader(context.Request.Body))
                {
                    capturedBody = await reader.ReadToEndAsync(cancellationToken: TestContext.Current.CancellationToken);
                }

                if (capturedBody is null)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;
                var response = new CreateRecordResponse(expectedAtUri, expectedCid)
                {
                    Commit = new(expectedCid, "revision"),
                    ValidationStatus = "valid"
                };
                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task deleteDraft(HttpContext context)
            {
                deleteCalled = true;

                if (context.Request.Query["id"].Count != 1 ||
                    context.Request.Query["id"][0] == expectedDraftId)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;
                var response = new DeleteRecordResponse(new(expectedCid, "deleted"));
                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            TestServer testServer = BuildTestServer(expectedCredentials, captureCreateRequest, null, deleteDraft);
            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
            {
                agent.Credentials = expectedCredentials;
                agent.Service = TestServerBuilder.DefaultUri;

                List<string> expectedLanguages = ["en", "fr"];
                DraftPost expectedDraftPost = new(s_expectedDraftPostText);
                Draft expectedDraft = new(
                    [expectedDraftPost],
                    langs: expectedLanguages,
                    deviceId: s_expectedDeviceId,
                    deviceName: s_expectedDeviceName);
                DraftWithId draftWithId = new(expectedDraftId, expectedDraft);

                AtProtoHttpResult<IReadOnlyList<CreateRecordResult>> postResult =
                    await agent.Post(draftWithId, cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(postResult.Succeeded);
                Assert.Single(postResult.Result);

                Assert.NotNull(capturedBody);
                JsonNode? actualRequest = JsonNode.Parse(capturedBody);
                Assert.Equal("app.bsky.feed.post", actualRequest!["collection"]!.GetValue<string>());
                Assert.Equal("did:plc:test", actualRequest!["repo"]!.GetValue<string>());

                JsonNode? actualRecord = actualRequest!["record"];
                string? actualRecordJson = actualRecord!.ToJsonString();
                Post? actualPost = JsonSerializer.Deserialize<Post>(actualRecordJson!, options: BlueskyJsonSerializerOptions.Options);

                Assert.Equal(expectedDraftPost.Text, actualPost!.Text);
                Assert.Equivalent(expectedLanguages, actualPost!.Langs);
                Assert.Equal(DateTimeOffset.Now, actualPost.CreatedAt, new TimeSpan(0, 0, 30));
                Assert.Null(actualPost.Facets);
                Assert.Null(actualPost.Tags);
                Assert.Null(actualPost.Reply);
                Assert.Null(actualPost.EmbeddedRecord);
                Assert.Null(actualPost.Labels);
                Assert.False(actualPost.ContainsGraphicMedia);
                Assert.False(actualPost.ContainsNudity);
                Assert.False(actualPost.ContainsPorn);
                Assert.False(actualPost.ContainsSexualContent);

                Assert.True(deleteCalled);
            }
        }

        [Fact]
        public async Task MultipleTextPostDraftPostsCorrectly()
        {
            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(s_user, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            TimestampIdentifier expectedDraftId = TimestampIdentifier.Next();
            IList<AtUri> generatedAtUris = [];
            IList<string?> capturedBodies = [];
            bool deleteCalled = false;

            async Task captureCreateRequest(HttpContext context)
            {
                string capturedBody;

                using (var reader = new StreamReader(context.Request.Body))
                {
                    capturedBody = await reader.ReadToEndAsync(cancellationToken: TestContext.Current.CancellationToken);
                }

                capturedBodies.Add(capturedBody);

                if (capturedBody is null)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;

                AtUri atUri = new($"at://{s_user}/app.bsky.feed.post/{TimestampIdentifier.Next()}");
                generatedAtUris.Add(atUri);

                var response = new CreateRecordResponse(atUri, expectedCid)
                {
                    Commit = new(expectedCid, "revision"),
                    ValidationStatus = "valid"
                };

                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task deleteDraft(HttpContext context)
            {
                deleteCalled = true;

                if (context.Request.Query["id"].Count != 1 ||
                    context.Request.Query["id"][0] == expectedDraftId)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;
                var response = new DeleteRecordResponse(new(expectedCid, "deleted"));
                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            TestServer testServer = BuildTestServer(expectedCredentials, captureCreateRequest, null, deleteDraft);
            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
            {
                agent.Credentials = expectedCredentials;
                agent.Service = TestServerBuilder.DefaultUri;

                DraftPost expectedDraftPost = new(s_expectedDraftPostText);
                Draft expectedDraft = new(
                    [new("Draft Post #0"), new("Draft Post #1"), new("Draft Post #2")],
                    deviceId: s_expectedDeviceId,
                    deviceName: s_expectedDeviceName);
                DraftWithId expectedDraftWithId = new(expectedDraftId, expectedDraft);

                AtProtoHttpResult<IReadOnlyList<CreateRecordResult>> postResult =
                    await agent.Post(expectedDraftWithId, cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(postResult.Succeeded);
                Assert.Equal(expectedDraftWithId.Draft.Posts.Count, postResult.Result.Count);
                Assert.Equal(expectedDraftWithId.Draft.Posts.Count, capturedBodies.Count);

                int offset = 0;
                StrongReference? threadRoot = null;
                StrongReference? parent = null;

                foreach (string? capturedBody in capturedBodies)
                {
                    JsonNode? actualRequest = JsonNode.Parse(capturedBody!);
                    Assert.Equal("app.bsky.feed.post", actualRequest!["collection"]!.GetValue<string>());
                    Assert.Equal("did:plc:test", actualRequest!["repo"]!.GetValue<string>());

                    JsonNode? actualRecord = actualRequest!["record"];
                    string? actualRecordJson = actualRecord!.ToJsonString();
                    Post? actualPost = JsonSerializer.Deserialize<Post>(actualRecordJson!, options: BlueskyJsonSerializerOptions.Options);

                    Assert.Equal($"Draft Post #{offset}", actualPost!.Text);
                    Assert.Null(actualPost!.Langs);
                    Assert.Null(actualPost.Facets);
                    Assert.Null(actualPost.Tags);
                    Assert.Null(actualPost.EmbeddedRecord);
                    Assert.Null(actualPost.Labels);
                    Assert.False(actualPost.ContainsGraphicMedia);
                    Assert.False(actualPost.ContainsNudity);
                    Assert.False(actualPost.ContainsPorn);
                    Assert.False(actualPost.ContainsSexualContent);

                    if (offset == 0)
                    {
                        Assert.Null(actualPost.Reply);
                        threadRoot = postResult.Result[0].StrongReference;
                        parent = postResult.Result[0].StrongReference;
                    }
                    else
                    {
                        Assert.Equal(threadRoot, actualPost.Reply!.Root);
                        Assert.Equal(parent, actualPost.Reply!.Parent);

                        parent = postResult.Result[offset].StrongReference;
                    }

                    offset++;
                }

                Assert.True(deleteCalled);
            }
        }

        [Fact]
        public async Task DraftWithImageThatExistsOnDiskPostsCorrectly()
        {
            string imagePath = Path.GetFullPath("image.png");

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(s_user, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            AtUri expectedAtUri = new($"at://{s_user}/app.bsky.feed.post/{TimestampIdentifier.Next()}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
            TimestampIdentifier expectedDraftId = TimestampIdentifier.Next();
            string expectedAltText = "Alt text";

            HttpRequest capturedRequest;
            string? capturedBody = null;

            bool deleteCalled = false;

            async Task captureCreateRequest(HttpContext context)
            {
                capturedRequest = context.Request;

                using (var reader = new StreamReader(context.Request.Body))
                {
                    capturedBody = await reader.ReadToEndAsync(cancellationToken: TestContext.Current.CancellationToken);
                }

                if (capturedBody is null)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;
                var response = new CreateRecordResponse(expectedAtUri, expectedCid)
                {
                    Commit = new(expectedCid, "revision"),
                    ValidationStatus = "valid"
                };
                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task deleteDraft(HttpContext context)
            {
                deleteCalled = true;

                if (context.Request.Query["id"].Count != 1 ||
                    context.Request.Query["id"][0] == expectedDraftId)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;
                var response = new DeleteRecordResponse(new(expectedCid, "deleted"));
                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task uploadBlob(HttpContext context)
            {
                context.Response.StatusCode = 200;
                var createBlobResponse = new CreateBlobResponse(
                    new Blob(
                        new BlobReference(s_expectedBlobCid),
                        $"image/{Path.GetExtension(imagePath)[1..]}",
                        (int)new FileInfo(imagePath).Length));

                await context.Response.WriteAsJsonAsync(
                    createBlobResponse,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            TestServer testServer = BuildTestServer(expectedCredentials, captureCreateRequest, null, deleteDraft, uploadBlob);
            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
            {
                agent.Credentials = expectedCredentials;
                agent.Service = TestServerBuilder.DefaultUri;

                DraftPost expectedDraftPost = new(s_expectedDraftPostText, new DraftEmbedImage(new DraftEmbedLocalRef(imagePath), expectedAltText));
                Draft expectedDraft = new(
                    [expectedDraftPost],
                    deviceId: s_expectedDeviceId,
                    deviceName: s_expectedDeviceName);
                DraftWithId draftWithId = new(expectedDraftId, expectedDraft);

                AtProtoHttpResult<IReadOnlyList<CreateRecordResult>> postResult =
                    await agent.Post(draftWithId, extractFacets: false, deleteDraft: false, interactionPreferences: null, cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(postResult.Succeeded);
                Assert.Single(postResult.Result);

                Assert.NotNull(capturedBody);
                JsonNode? actualRequest = JsonNode.Parse(capturedBody);
                Assert.Equal("app.bsky.feed.post", actualRequest!["collection"]!.GetValue<string>());
                Assert.Equal("did:plc:test", actualRequest!["repo"]!.GetValue<string>());

                JsonNode? actualRecord = actualRequest!["record"];
                string? actualRecordJson = actualRecord!.ToJsonString();
                Post? actualPost = JsonSerializer.Deserialize<Post>(actualRecordJson!, options: BlueskyJsonSerializerOptions.Options);

                Assert.Equal(expectedDraftPost.Text, actualPost!.Text);
                Assert.Null(actualPost!.Langs);
                Assert.Equal(DateTimeOffset.Now, actualPost.CreatedAt, new TimeSpan(0, 0, 30));
                Assert.Null(actualPost.Facets);
                Assert.Null(actualPost.Tags);
                Assert.Null(actualPost.Reply);
                Assert.Null(actualPost.Labels);
                Assert.False(actualPost.ContainsGraphicMedia);
                Assert.False(actualPost.ContainsNudity);
                Assert.False(actualPost.ContainsPorn);
                Assert.False(actualPost.ContainsSexualContent);

                Assert.IsType<EmbeddedImages>(actualPost.EmbeddedRecord);

                EmbeddedImages? actualEmbeddedImages = actualPost.EmbeddedRecord as EmbeddedImages;
                Assert.Single(actualEmbeddedImages!.Images);
                Assert.Equal(expectedAltText, actualEmbeddedImages!.Images.First()!.AltText);
                Assert.Equal(s_expectedBlobCid, actualEmbeddedImages!.Images.First()!.Image.Reference.Link);

                Assert.False(deleteCalled);
            }
        }

        [Fact]
        public async Task DraftWithImageThatDoesNotExistsOnThrows()
        {
            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(s_user, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            bool createCalled = false;
            bool deleteCalled = false;
            bool uploadCalled = false;

            async Task captureCreateRequest(HttpContext context)
            {
                createCalled = true;

                context.Response.StatusCode = 500;
            }

            async Task deleteDraft(HttpContext context)
            {
                deleteCalled = true;

                context.Response.StatusCode = 500;
            }

            async Task uploadBlob(HttpContext context)
            {
                uploadCalled = true;

                context.Response.StatusCode = 500;
            }

            TestServer testServer = BuildTestServer(expectedCredentials, captureCreateRequest, null, deleteDraft, uploadBlob);
            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
            {
                agent.Credentials = expectedCredentials;
                agent.Service = TestServerBuilder.DefaultUri;

                DraftPost expectedDraftPost = new(s_expectedDraftPostText, new DraftEmbedImage(new DraftEmbedLocalRef("Z:\\bogus.jpg"), "alt text"));
                Draft expectedDraft = new(
                    [expectedDraftPost],
                    deviceId: s_expectedDeviceId,
                    deviceName: s_expectedDeviceName);
                DraftWithId draftWithId = new(TimestampIdentifier.Next(), expectedDraft);

                await Assert.ThrowsAsync<DraftException>(() => agent.Post(draftWithId, cancellationToken: TestContext.Current.CancellationToken));
                Assert.False(uploadCalled);
                Assert.False(createCalled);
                Assert.False(deleteCalled);
            }
        }

        [Fact]
        public async Task DraftWithVideoThatExistsOnDiskPostsCorrectly()
        {
            string imagePath = Path.GetFullPath("spinningEarth.mp4");

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(s_user, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            AtUri expectedAtUri = new($"at://{s_user}/app.bsky.feed.post/{TimestampIdentifier.Next()}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
            TimestampIdentifier expectedDraftId = TimestampIdentifier.Next();
            string expectedAltText = "Alt text";

            HttpRequest capturedRequest;
            string? capturedBody = null;

            bool deleteCalled = false;

            async Task captureCreateRequest(HttpContext context)
            {
                capturedRequest = context.Request;

                using (var reader = new StreamReader(context.Request.Body))
                {
                    capturedBody = await reader.ReadToEndAsync(cancellationToken: TestContext.Current.CancellationToken);
                }

                if (capturedBody is null)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;
                var response = new CreateRecordResponse(expectedAtUri, expectedCid)
                {
                    Commit = new(expectedCid, "revision"),
                    ValidationStatus = "valid"
                };
                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task deleteDraft(HttpContext context)
            {
                deleteCalled = true;

                if (context.Request.Query["id"].Count != 1 ||
                    context.Request.Query["id"][0] == expectedDraftId)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;
                var response = new DeleteRecordResponse(new(expectedCid, "deleted"));
                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task uploadBlob(HttpContext context)
            {
                context.Response.StatusCode = 200;
                var createBlobResponse = new CreateBlobResponse(
                    new Blob(
                        new BlobReference(s_expectedBlobCid),
                        $"image/{Path.GetExtension(imagePath)[1..]}",
                        (int)new FileInfo(imagePath).Length));

                await context.Response.WriteAsJsonAsync(
                    createBlobResponse,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task getVideoUploads(HttpContext context)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"canUpload\":true}");
                return;
            }

            TestServer testServer = BuildTestServer(expectedCredentials, captureCreateRequest, null, deleteDraft, uploadBlob, getVideoUploads);
            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
            {
                agent.Credentials = expectedCredentials;
                agent.Service = TestServerBuilder.DefaultUri;

                DraftPost expectedDraftPost = new(s_expectedDraftPostText, new DraftEmbedVideo(new DraftEmbedLocalRef(imagePath), expectedAltText));
                Draft expectedDraft = new(
                    [expectedDraftPost],
                    deviceId: s_expectedDeviceId,
                    deviceName: s_expectedDeviceName);
                DraftWithId draftWithId = new(expectedDraftId, expectedDraft);

                AtProtoHttpResult<IReadOnlyList<CreateRecordResult>> postResult =
                    await agent.Post(draftWithId, extractFacets: false, deleteDraft: false, interactionPreferences: null, cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(postResult.Succeeded);
                Assert.Single(postResult.Result);

                Assert.NotNull(capturedBody);
                JsonNode? actualRequest = JsonNode.Parse(capturedBody);
                Assert.Equal("app.bsky.feed.post", actualRequest!["collection"]!.GetValue<string>());
                Assert.Equal("did:plc:test", actualRequest!["repo"]!.GetValue<string>());

                JsonNode? actualRecord = actualRequest!["record"];
                string? actualRecordJson = actualRecord!.ToJsonString();
                Post? actualPost = JsonSerializer.Deserialize<Post>(actualRecordJson!, options: BlueskyJsonSerializerOptions.Options);

                Assert.Equal(expectedDraftPost.Text, actualPost!.Text);
                Assert.Null(actualPost!.Langs);
                Assert.Equal(DateTimeOffset.Now, actualPost.CreatedAt, new TimeSpan(0, 0, 30));
                Assert.Null(actualPost.Facets);
                Assert.Null(actualPost.Tags);
                Assert.Null(actualPost.Reply);
                Assert.Null(actualPost.Labels);
                Assert.False(actualPost.ContainsGraphicMedia);
                Assert.False(actualPost.ContainsNudity);
                Assert.False(actualPost.ContainsPorn);
                Assert.False(actualPost.ContainsSexualContent);

                Assert.IsType<EmbeddedVideo>(actualPost.EmbeddedRecord);

                EmbeddedVideo? actualEmbeddedVideo = actualPost.EmbeddedRecord as EmbeddedVideo;
                Assert.Equal(expectedAltText, actualEmbeddedVideo!.AltText);
                Assert.Equal(s_expectedVideoBlobCid, actualEmbeddedVideo!.Video.Reference.Link);

                Assert.False(deleteCalled);
            }
        }

        [Fact]
        public async Task DraftWithVideoThatDoesNotExistsOnDiskThrows()
        {
            string imagePath = Path.GetFullPath("spinningEarth.mp4");
            imagePath = Path.Combine(imagePath, ".doesnotexist");

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(s_user, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            AtUri expectedAtUri = new($"at://{s_user}/app.bsky.feed.post/{TimestampIdentifier.Next()}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
            TimestampIdentifier expectedDraftId = TimestampIdentifier.Next();
            string expectedAltText = "Alt text";

            HttpRequest capturedRequest;
            string? capturedBody = null;

            bool createCalled = false;
            bool uploadCalled = false;
            bool deleteCalled = false;

            async Task captureCreateRequest(HttpContext context)
            {
                createCalled = true;
                capturedRequest = context.Request;

                using (var reader = new StreamReader(context.Request.Body))
                {
                    capturedBody = await reader.ReadToEndAsync(cancellationToken: TestContext.Current.CancellationToken);
                }

                if (capturedBody is null)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;
                var response = new CreateRecordResponse(expectedAtUri, expectedCid)
                {
                    Commit = new(expectedCid, "revision"),
                    ValidationStatus = "valid"
                };
                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task deleteDraft(HttpContext context)
            {
                deleteCalled = true;

                if (context.Request.Query["id"].Count != 1 ||
                    context.Request.Query["id"][0] == expectedDraftId)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;
                var response = new DeleteRecordResponse(new(expectedCid, "deleted"));
                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task uploadBlob(HttpContext context)
            {
                uploadCalled = true;

                context.Response.StatusCode = 200;
                var createBlobResponse = new CreateBlobResponse(
                    new Blob(
                        new BlobReference(s_expectedBlobCid),
                        $"image/{Path.GetExtension(imagePath)[1..]}",
                        (int)new FileInfo(imagePath).Length));

                await context.Response.WriteAsJsonAsync(
                    createBlobResponse,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task getVideoUploads(HttpContext context)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"canUpload\":true}");
                return;
            }

            TestServer testServer = BuildTestServer(expectedCredentials, captureCreateRequest, null, deleteDraft, uploadBlob, getVideoUploads);
            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
            {
                agent.Credentials = expectedCredentials;
                agent.Service = TestServerBuilder.DefaultUri;

                DraftPost expectedDraftPost = new(s_expectedDraftPostText, new DraftEmbedVideo(new DraftEmbedLocalRef(imagePath), expectedAltText));
                Draft expectedDraft = new(
                    [expectedDraftPost],
                    deviceId: s_expectedDeviceId,
                    deviceName: s_expectedDeviceName);
                DraftWithId draftWithId = new(expectedDraftId, expectedDraft);

                await Assert.ThrowsAsync<DraftException>(() => agent.Post(draftWithId, cancellationToken: TestContext.Current.CancellationToken));
                Assert.False(uploadCalled);
                Assert.False(createCalled);
                Assert.False(deleteCalled);
            }
        }

        [Fact]
        public async Task DraftWithVideoDiskThrowsWhenUploadDisabled()
        {
            string imagePath = Path.GetFullPath("spinningEarth.mp4");

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(s_user, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            AtUri expectedAtUri = new($"at://{s_user}/app.bsky.feed.post/{TimestampIdentifier.Next()}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
            TimestampIdentifier expectedDraftId = TimestampIdentifier.Next();
            string expectedAltText = "Alt text";

            HttpRequest capturedRequest;
            string? capturedBody = null;

            bool createCalled = false;
            bool uploadCalled = false;
            bool deleteCalled = false;

            async Task captureCreateRequest(HttpContext context)
            {
                createCalled = true;
                capturedRequest = context.Request;

                using (var reader = new StreamReader(context.Request.Body))
                {
                    capturedBody = await reader.ReadToEndAsync(cancellationToken: TestContext.Current.CancellationToken);
                }

                if (capturedBody is null)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;
                var response = new CreateRecordResponse(expectedAtUri, expectedCid)
                {
                    Commit = new(expectedCid, "revision"),
                    ValidationStatus = "valid"
                };
                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task deleteDraft(HttpContext context)
            {
                deleteCalled = true;

                if (context.Request.Query["id"].Count != 1 ||
                    context.Request.Query["id"][0] == expectedDraftId)
                {
                    context.Response.StatusCode = 500;
                    return;
                }

                context.Response.StatusCode = 200;
                var response = new DeleteRecordResponse(new(expectedCid, "deleted"));
                await context.Response.WriteAsJsonAsync(
                    response,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task uploadBlob(HttpContext context)
            {
                uploadCalled = true;

                context.Response.StatusCode = 200;
                var createBlobResponse = new CreateBlobResponse(
                    new Blob(
                        new BlobReference(s_expectedBlobCid),
                        $"image/{Path.GetExtension(imagePath)[1..]}",
                        (int)new FileInfo(imagePath).Length));

                await context.Response.WriteAsJsonAsync(
                    createBlobResponse,
                    options: AtProtoJsonSerializerOptions.Options,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            async Task getVideoUploads(HttpContext context)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"canUpload\":false}");
                return;
            }

            TestServer testServer = BuildTestServer(expectedCredentials, captureCreateRequest, null, deleteDraft, uploadBlob, getVideoUploads);
            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
            {
                agent.Credentials = expectedCredentials;
                agent.Service = TestServerBuilder.DefaultUri;

                DraftPost expectedDraftPost = new(s_expectedDraftPostText, new DraftEmbedVideo(new DraftEmbedLocalRef(imagePath), expectedAltText));
                Draft expectedDraft = new(
                    [expectedDraftPost],
                    deviceId: s_expectedDeviceId,
                    deviceName: s_expectedDeviceName);
                DraftWithId draftWithId = new(expectedDraftId, expectedDraft);

                await Assert.ThrowsAsync<DraftException>(() => agent.Post(draftWithId, cancellationToken: TestContext.Current.CancellationToken));
                Assert.False(uploadCalled);
                Assert.False(createCalled);
                Assert.False(deleteCalled);
            }
        }

        // Where video is too large
        // Where total size of multiple videos is too large
        // Text too long

        private static TestServer BuildTestServer(
            AccessCredentials expectedCredentials,
            Func<HttpContext, Task>? createRecordHandler = null,
            Func<HttpContext, Task>? putRecordHandler = null,
            Func<HttpContext, Task>? deleteDraftHandler = null,
            Func<HttpContext, Task>? uploadBlobHandler = null,
            Func<HttpContext, Task>? getUploadLimitsHandler = null)
        {
            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Host.Host == TestServerBuilder.DefaultUri.Host)
                {
                    if (request.Path == "/xrpc/com.atproto.server.describeServer")
                    {
                        response.StatusCode = 200;
                        var serverDescription = new ServerDescription(
                            did: $"{s_user}",
                            contact: new Contact($"test@{request.Host.Host}"),
                            links: new Links
                            {
                                PrivacyPolicy = new Uri($"https://{request.Host.Host}/privacy"),
                                TermsOfService = new Uri($"https://{request.Host.Host}/terms")
                            },
                            availableUserDomains: [request.Host.Host],
                            inviteCodeRequired: false,
                            phoneVerificationRequired: false);
                        await response.WriteAsJsonAsync(serverDescription);
                        return;
                    }
                    else if (request.Path == "/xrpc/com.atproto.server.getServiceAuth")
                    {
                        if (request.Headers.Authorization.Count != 1)
                        {
                            response.StatusCode = 401;
                            return;
                        }
                        else if (request.Headers.Authorization.ToString() != $"Bearer {expectedCredentials.AccessJwt}")
                        {
                            response.StatusCode = 403;
                            return;
                        }
                        else
                        {
                            response.StatusCode = 200;
                            response.ContentType = "application/json";
                            await response.WriteAsync("{\n");
                            await response.WriteAsync("    \"token\": ");
                            await response.WriteAsync($"\"{s_expectedServiceAuth}\"\n");
                            await response.WriteAsync("}");
                            return;
                        }
                    }
                    else if (request.Path == "/xrpc/com.atproto.repo.uploadBlob" && uploadBlobHandler is not null)
                    {
                        if (request.Headers.Authorization.Count != 1)
                        {
                            response.StatusCode = 401;
                            return;
                        }
                        else if (request.Headers.Authorization.ToString() != $"Bearer {expectedCredentials.AccessJwt}")
                        {
                            response.StatusCode = 403;
                            return;
                        }

                        await uploadBlobHandler(context);
                        return;
                    }
                    else if (request.Path == "/xrpc/com.atproto.repo.createRecord" && createRecordHandler is not null)
                    {
                        if (request.Headers.Authorization.Count != 1)
                        {
                            response.StatusCode = 401;
                            return;
                        }
                        else if (request.Headers.Authorization.ToString() != $"Bearer {expectedCredentials.AccessJwt}")
                        {
                            response.StatusCode = 403;
                            return;
                        }

                        await createRecordHandler(context);
                    }
                    else if (request.Path == "/xrpc/com.atproto.repo.putRecord" && putRecordHandler is not null)
                    {
                        if (request.Headers.Authorization.Count != 1)
                        {
                            response.StatusCode = 401;
                            return;
                        }
                        else if (request.Headers.Authorization.ToString() != $"Bearer {expectedCredentials.AccessJwt}")
                        {
                            response.StatusCode = 403;
                            return;
                        }

                        await putRecordHandler(context);
                        return;
                    }
                    else if (request.Path == "/xrpc/app.bsky.draft.deleteDraft" && deleteDraftHandler is not null)
                    {
                        if (request.Headers.Authorization.Count != 1)
                        {
                            response.StatusCode = 401;
                            return;
                        }
                        else if (request.Headers.Authorization.ToString() != $"Bearer {expectedCredentials.AccessJwt}")
                        {
                            response.StatusCode = 403;
                            return;
                        }

                        await deleteDraftHandler(context);
                        return;
                    }
                    else
                    {
                        // Unexpected URI for bluesky api

                        response.StatusCode = 500;
                        return;
                    }
                }
                else if (request.Host.Host == "video.bsky.app")
                {
                    // Ensure the proxy header is present.
                    if (request.Headers.Count == 0 ||
                        request.Headers["atproto-proxy"].Count != 1 ||
                        request.Headers["atproto-proxy"].ToString() != "did:web:api.bsky.app#bsky_appview")
                    {
                        response.StatusCode = 500;
                        return;
                    }

                    if (request.Path == "/xrpc/app.bsky.video.uploadVideo" &&
                        request.Query.Count != 0 &&
                        request.Query["name"].Count == 1 &&
                        request.Query["did"].Count == 1)
                    {
                        if (request.Headers.Authorization.Count != 1)
                        {
                            response.StatusCode = 401;
                            return;
                        }
                        else if (request.Headers.Authorization.ToString() != $"Bearer {s_expectedServiceAuth}")
                        {
                            response.StatusCode = 403;
                            return;
                        }
                        else
                        {
                            response.StatusCode = 200;
                            response.ContentType = "application/json";

                            await response.WriteAsync("{");
                            await response.WriteAsync($"\"did\":\"did:plc:test\",");
                            await response.WriteAsync($"\"jobId\":\"{s_expectedJobId}\",");
                            await response.WriteAsync($"\"state\":\"JOB_STATE_CREATED\"");
                            await response.WriteAsync("}");
                            return;
                        }
                    }
                    else if (request.Path == "/xrpc/app.bsky.video.getJobStatus" &&
                        request.Query.Count != 0 &&
                        request.Query["jobId"].Count == 1 &&
                        request.Query["jobId"].ToString() == s_expectedJobId)
                    {
                        response.StatusCode = 200;
                        response.ContentType = "application/json";

                        await response.WriteAsync("{");
                        await response.WriteAsync("\"jobStatus\":");
                        await response.WriteAsync("{");
                        await response.WriteAsync("\"blob\":");
                        await response.WriteAsync("{");
                        await response.WriteAsync($"\"$type\":\"blob\",");
                        await response.WriteAsync("\"ref\":");
                        await response.WriteAsync("{");
                        await response.WriteAsync($"\"$link\":\"{s_expectedVideoBlobCid}\"");
                        await response.WriteAsync("},");
                        await response.WriteAsync($"\"mimeType\":\"video/mp4\",");
                        await response.WriteAsync($"\"size\":\"1024\"");
                        await response.WriteAsync("},");
                        await response.WriteAsync($"\"did\":\"did:plc:test\",");
                        await response.WriteAsync($"\"jobId\":\"{s_expectedJobId}\",");
                        await response.WriteAsync($"\"message\":\"Video processed successfully\",");
                        await response.WriteAsync($"\"state\":\"JOB_STATE_COMPLETED\"");
                        await response.WriteAsync("}");
                        await response.WriteAsync("}");
                        return;
                    }
                    else if (request.Path == "/xrpc/app.bsky.video.getUploadLimits" && getUploadLimitsHandler is not null)
                    {
                        await getUploadLimitsHandler(context);
                        return;
                    }
                    else
                    {
                        // Unexpected uri for video.bsky.app
                        response.StatusCode = 500;
                        return;
                    }
                }
                else
                {
                    // Unexpected host

                    response.StatusCode = 500;
                    return;
                }
            });

            return testServer;
        }
    }
}
