// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

public class StandardSiteEmbeddedCardGeneratorTests
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public StandardSiteEmbeddedCardGeneratorTests()
    {
        _jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    [Fact]
    public async Task OpenGraphFullMetadataButNoStandardSiteMetadataCreatesCorrectCard()
    {
        Did expectedDid = "did:plc:test";

        Cid expectedBlobCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

        const string expectedCardUrl = "https://example.com/test-page";
        const string expectedCardTitle = "Test Document";
        const string expectedCardDescription = "This is a test document for Open Graph metadata.";

        AccessCredentials expectedCredentials = new(
                service: TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                refreshToken: "refreshToken");

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == TestServerBuilder.DefaultUri.Host)
            {
                if (request.Path == "/document/12345")
                {
                    await response.WriteAsync(@"<!DOCTYPE html>");
                    await response.WriteAsync(@"<html>");
                    await response.WriteAsync(@"<head>");
                    await response.WriteAsync($@"<title>{expectedCardTitle}</title>");
                    await response.WriteAsync($@"<meta property=""og:title"" content=""{expectedCardTitle}"" />");
                    await response.WriteAsync($@"<meta property=""og:description"" content=""{expectedCardDescription}"" />");
                    await response.WriteAsync($@"<meta property=""og:url"" content=""{expectedCardUrl}"" />");
                    await response.WriteAsync($@"<meta property=""og:image"" content=""{TestServerBuilder.DefaultUri}document/12345/image.png"" />");
                    await response.WriteAsync(@"</head>");
                    await response.WriteAsync(@"<body>");
                    await response.WriteAsync($@"<h1>{expectedCardTitle}</h1>");
                    await response.WriteAsync(@"</body>");
                    await response.WriteAsync(@"</html>");
                    return;
                }

                if (request.Path == "/document/12345/image.png")
                {
                    await response.SendFileAsync("image.png");
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.uploadBlob")
                {
                    string json = "{\"blob\":{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"},\"mimeType\":\"image/png\",\"size\":999}}";

                    response.ContentType = "application/json";
                    await response.WriteAsync(json);
                    return;
                }
            }
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = expectedCredentials;

            Uri uri = new($"{TestServerBuilder.DefaultUri}document/12345");
            StandardSiteCardGenerator standardSiteCardGenerator = agent.CreateStandardSiteEmbeddedCardGenerator();

            Post post = new()
            {
                Text = $"Check out this document: {uri}"
            };

            EmbeddedExternal? card = await standardSiteCardGenerator.Generate(uri, TestContext.Current.CancellationToken);

            Assert.NotNull(card);

            Assert.Equal(expectedCardUrl, card.External.Uri.ToString());
            Assert.Equal(expectedCardTitle, card.External.Title);
            Assert.Equal(expectedCardDescription, card.External.Description);
            Assert.Equal(expectedBlobCid, card.External.Thumbnail!.Reference!.Link);
            Assert.Equal("image/png", card.External.Thumbnail.MimeType);
            Assert.Equal(999, card.External.Thumbnail.Size);
        }
    }

    [Fact]
    public async Task FullSiteStandardPageMetadataAndBackingRecordsCreatesCorrectCard()
    {
        Did expectedDid = "did:plc:test";
        Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
        RecordKey expectedDocumentRKey = new("3mn4upg7a4z2h");
        RecordKey expectedPublicationRKey = new("3meddhkrg5z2p");

        AtUri expectedStandardSiteDocument = new ($"at://{expectedDid}/site.standard.document/{expectedDocumentRKey}");
        AtUri expectedStandardSitePublication = new($"at://{expectedDid}/site.standard.publication/{expectedPublicationRKey}");

        const string expectedCardUrl = "https://example.com/test-page";
        const string expectedCardTitle = "Test Document";
        const string expectedCardDescription = "This is a test document for Open Graph metadata.";

        AccessCredentials expectedCredentials = new(
                service: TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                refreshToken: "refreshToken");

        int didDocumentRequestCount = 0;
        int wellKnownPublicationDocumentRequestCount = 0;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == TestServerBuilder.DefaultUri.Host)
            {
                if (request.Path == "/document/12345")
                {
                    await response.WriteAsync(@"<!DOCTYPE html>");
                    await response.WriteAsync(@"<html>");
                    await response.WriteAsync(@"<head>");
                    await response.WriteAsync($@"<title>{expectedCardTitle}</title>");
                    await response.WriteAsync($@"<meta property=""og:title"" content=""{expectedCardTitle}"" />");
                    await response.WriteAsync($@"<meta property=""og:description"" content=""{expectedCardDescription}"" />");
                    await response.WriteAsync($@"<meta property=""og:url"" content=""{expectedCardUrl}"" />");
                    await response.WriteAsync($@"<meta property=""og:image"" content=""{TestServerBuilder.DefaultUri}document/12345/image.png"" />");
                    await response.WriteAsync($@"<link rel=""site.standard.document"" href=""{expectedStandardSiteDocument}"" />");
                    await response.WriteAsync($@"<link rel=""site.standard.publication"" href=""{expectedStandardSitePublication}"" />");
                    await response.WriteAsync(@"</head>");
                    await response.WriteAsync(@"<body>");
                    await response.WriteAsync($@"<h1>{expectedCardTitle}</h1>");
                    await response.WriteAsync(@"</body>");
                    await response.WriteAsync(@"</html>");
                    return;
                }

                if (request.Path == "/document/12345/image.png")
                {
                    await response.SendFileAsync("image.png");
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.uploadBlob")
                {
                    string json = "{\"blob\":{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"},\"mimeType\":\"image/png\",\"size\":999}}";

                    response.ContentType = "application/json";
                    await response.WriteAsync(json);
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.getRecord" )
                {
                    if (request.Query["repo"].FirstOrDefault()!.Equals(expectedDid))
                    {
                        string collection = request.Query["collection"].FirstOrDefault() ?? string.Empty;
                        string rkey = request.Query["rkey"].FirstOrDefault() ?? string.Empty;

                        if (collection.Equals("site.standard.document") &&
                            rkey.Equals(expectedDocumentRKey.ToString()))
                        {
                            response.ContentType = "application/json";

                            string content = $"{{\"uri\":\"{expectedStandardSiteDocument}\",\"cid\":\"{expectedCid}\",\"value\":{{\"test\":true}}}}";
                            await response.WriteAsync(content);
                            return;
                        }
                        else if (collection.Equals("site.standard.publication") &&
                                 rkey.Equals(expectedPublicationRKey.ToString()))
                        {
                            response.ContentType = "application/json";
                            string content = $"{{\"uri\":\"{expectedStandardSitePublication}\",\"cid\":\"{expectedCid}\",\"value\":{{\"test\":true}}}}";
                            await response.WriteAsync(content);
                            return;
                        }
                    }
                    response.StatusCode = 404;
                    return;
                }

                if (request.Path == "/.well-known/site.standard.publication")
                {
                    wellKnownPublicationDocumentRequestCount++;
                    await response.WriteAsync(expectedStandardSitePublication.ToString());
                    return;
                }
            }
            else if (request.Host.Host == "plc.directory")
            {
                didDocumentRequestCount++;

                if (request.Path == $"/{expectedDid}")
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{expectedDid}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                            id : "#atproto_pds",
                            type : "atprotopds",
                            serviceEndpoint : TestServerBuilder.DefaultUri
                            )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
            }
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = expectedCredentials;

            Uri uri = new($"{TestServerBuilder.DefaultUri}document/12345");
            StandardSiteCardGenerator standardSiteCardGenerator = agent.CreateStandardSiteEmbeddedCardGenerator();

            Post post = new()
            {
                Text = $"Check out this document: {uri}"
            };

            EmbeddedExternal? card = await standardSiteCardGenerator.Generate(uri, TestContext.Current.CancellationToken);

            Assert.NotNull(card);

            Assert.Equal(expectedCardUrl, card.External.Uri.ToString());
            Assert.Equal(expectedCardTitle, card.External.Title);
            Assert.Equal(expectedCardDescription, card.External.Description);
            Assert.Equal(expectedCid, card.External.Thumbnail!.Reference!.Link);
            Assert.Equal("image/png", card.External.Thumbnail.MimeType);
            Assert.Equal(999, card.External.Thumbnail.Size);

            Assert.NotNull(card.External.AssociatedRefs);
            Assert.Equal(2, card.External.AssociatedRefs.Count);
            Assert.Contains(new StrongReference(expectedStandardSiteDocument, expectedCid), card.External.AssociatedRefs);
            Assert.Contains(new StrongReference(expectedStandardSitePublication, expectedCid), card.External.AssociatedRefs);

            Assert.Equal(1, didDocumentRequestCount);
            Assert.Equal(0, wellKnownPublicationDocumentRequestCount);
        }
    }

    [Fact]
    public async Task SiteStandardPageMetadataButNoPublicationMetadataAndBackingRecordsFallsBackToWellKnownResolutionAndCreatesCorrectCard()
    {
        Did expectedDid = "did:plc:test";
        Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
        RecordKey expectedDocumentRKey = new("3mn4upg7a4z2h");
        RecordKey expectedPublicationRKey = new("3meddhkrg5z2p");

        AtUri expectedStandardSiteDocument = new($"at://{expectedDid}/site.standard.document/{expectedDocumentRKey}");
        AtUri expectedStandardSitePublication = new($"at://{expectedDid}/site.standard.publication/{expectedPublicationRKey}");

        const string expectedCardUrl = "https://example.com/test-page";
        const string expectedCardTitle = "Test Document";
        const string expectedCardDescription = "This is a test document for Open Graph metadata.";

        AccessCredentials expectedCredentials = new(
                service: TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                refreshToken: "refreshToken");

        int didDocumentRequestCount = 0;
        int wellKnownPublicationDocumentRequestCount = 0;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == TestServerBuilder.DefaultUri.Host)
            {
                if (request.Path == "/document/12345")
                {
                    await response.WriteAsync(@"<!DOCTYPE html>");
                    await response.WriteAsync(@"<html>");
                    await response.WriteAsync(@"<head>");
                    await response.WriteAsync($@"<title>{expectedCardTitle}</title>");
                    await response.WriteAsync($@"<meta property=""og:title"" content=""{expectedCardTitle}"" />");
                    await response.WriteAsync($@"<meta property=""og:description"" content=""{expectedCardDescription}"" />");
                    await response.WriteAsync($@"<meta property=""og:url"" content=""{expectedCardUrl}"" />");
                    await response.WriteAsync($@"<meta property=""og:image"" content=""{TestServerBuilder.DefaultUri}document/12345/image.png"" />");
                    await response.WriteAsync($@"<link rel=""site.standard.document"" href=""{expectedStandardSiteDocument}"" />");
                    await response.WriteAsync(@"</head>");
                    await response.WriteAsync(@"<body>");
                    await response.WriteAsync($@"<h1>{expectedCardTitle}</h1>");
                    await response.WriteAsync(@"</body>");
                    await response.WriteAsync(@"</html>");
                    return;
                }

                if (request.Path == "/document/12345/image.png")
                {
                    await response.SendFileAsync("image.png");
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.uploadBlob")
                {
                    string json = "{\"blob\":{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"},\"mimeType\":\"image/png\",\"size\":999}}";

                    response.ContentType = "application/json";
                    await response.WriteAsync(json);
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.getRecord")
                {
                    if (request.Query["repo"].FirstOrDefault()!.Equals(expectedDid))
                    {
                        string collection = request.Query["collection"].FirstOrDefault() ?? string.Empty;
                        string rkey = request.Query["rkey"].FirstOrDefault() ?? string.Empty;

                        if (collection.Equals("site.standard.document") &&
                            rkey.Equals(expectedDocumentRKey.ToString()))
                        {
                            response.ContentType = "application/json";

                            string content = $"{{\"uri\":\"{expectedStandardSiteDocument}\",\"cid\":\"{expectedCid}\",\"value\":{{\"test\":true}}}}";
                            await response.WriteAsync(content);
                            return;
                        }
                        else if (collection.Equals("site.standard.publication") &&
                                 rkey.Equals(expectedPublicationRKey.ToString()))
                        {
                            response.ContentType = "application/json";
                            string content = $"{{\"uri\":\"{expectedStandardSitePublication}\",\"cid\":\"{expectedCid}\",\"value\":{{\"test\":true}}}}";
                            await response.WriteAsync(content);
                            return;
                        }
                    }
                    response.StatusCode = 404;
                    return;
                }

                if (request.Path == "/.well-known/site.standard.publication")
                {
                    wellKnownPublicationDocumentRequestCount++;
                    await response.WriteAsync(expectedStandardSitePublication.ToString());
                    return;
                }
            }
            else if (request.Host.Host == "plc.directory")
            {
                didDocumentRequestCount++;

                if (request.Path == $"/{expectedDid}")
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{expectedDid}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                            id : "#atproto_pds",
                            type : "atprotopds",
                            serviceEndpoint : TestServerBuilder.DefaultUri
                            )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
            }
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = expectedCredentials;

            Uri uri = new($"{TestServerBuilder.DefaultUri}document/12345");
            StandardSiteCardGenerator standardSiteCardGenerator = agent.CreateStandardSiteEmbeddedCardGenerator();

            Post post = new()
            {
                Text = $"Check out this document: {uri}"
            };

            EmbeddedExternal? card = await standardSiteCardGenerator.Generate(uri, TestContext.Current.CancellationToken);

            Assert.NotNull(card);

            Assert.Equal(expectedCardUrl, card.External.Uri.ToString());
            Assert.Equal(expectedCardTitle, card.External.Title);
            Assert.Equal(expectedCardDescription, card.External.Description);
            Assert.Equal(expectedCid, card.External.Thumbnail!.Reference!.Link);
            Assert.Equal("image/png", card.External.Thumbnail.MimeType);
            Assert.Equal(999, card.External.Thumbnail.Size);

            Assert.NotNull(card.External.AssociatedRefs);
            Assert.Equal(2, card.External.AssociatedRefs.Count);
            Assert.Contains(new StrongReference(expectedStandardSiteDocument, expectedCid), card.External.AssociatedRefs);
            Assert.Contains(new StrongReference(expectedStandardSitePublication, expectedCid), card.External.AssociatedRefs);

            Assert.Equal(1, didDocumentRequestCount);
            Assert.Equal(1, wellKnownPublicationDocumentRequestCount);
        }
    }

    [Fact]
    public async Task FullSiteStandardPageMetadataWithNoBackingRecordsDoesNotAddStandardStandedMetadataToCard()
    {
        Did expectedDid = "did:plc:test";
        Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
        RecordKey expectedDocumentRKey = new("3mn4upg7a4z2h");
        RecordKey expectedPublicationRKey = new("3meddhkrg5z2p");

        AtUri expectedStandardSiteDocument = new($"at://{expectedDid}/site.standard.document/{expectedDocumentRKey}");
        AtUri expectedStandardSitePublication = new($"at://{expectedDid}/site.standard.publication/{expectedPublicationRKey}");

        const string expectedCardUrl = "https://example.com/test-page";
        const string expectedCardTitle = "Test Document";
        const string expectedCardDescription = "This is a test document for Open Graph metadata.";

        AccessCredentials expectedCredentials = new(
                service: TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                refreshToken: "refreshToken");

        int didDocumentRequestCount = 0;
        int wellKnownPublicationDocumentRequestCount = 0;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == TestServerBuilder.DefaultUri.Host)
            {
                if (request.Path == "/document/12345")
                {
                    await response.WriteAsync(@"<!DOCTYPE html>");
                    await response.WriteAsync(@"<html>");
                    await response.WriteAsync(@"<head>");
                    await response.WriteAsync($@"<title>{expectedCardTitle}</title>");
                    await response.WriteAsync($@"<meta property=""og:title"" content=""{expectedCardTitle}"" />");
                    await response.WriteAsync($@"<meta property=""og:description"" content=""{expectedCardDescription}"" />");
                    await response.WriteAsync($@"<meta property=""og:url"" content=""{expectedCardUrl}"" />");
                    await response.WriteAsync($@"<meta property=""og:image"" content=""{TestServerBuilder.DefaultUri}document/12345/image.png"" />");
                    await response.WriteAsync($@"<link rel=""site.standard.document"" href=""{expectedStandardSiteDocument}"" />");
                    await response.WriteAsync($@"<link rel=""site.standard.publication"" href=""{expectedStandardSitePublication}"" />");
                    await response.WriteAsync(@"</head>");
                    await response.WriteAsync(@"<body>");
                    await response.WriteAsync($@"<h1>{expectedCardTitle}</h1>");
                    await response.WriteAsync(@"</body>");
                    await response.WriteAsync(@"</html>");
                    return;
                }

                if (request.Path == "/document/12345/image.png")
                {
                    await response.SendFileAsync("image.png");
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.uploadBlob")
                {
                    string json = "{\"blob\":{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"},\"mimeType\":\"image/png\",\"size\":999}}";

                    response.ContentType = "application/json";
                    await response.WriteAsync(json);
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.getRecord")
                {
                    response.StatusCode = 404;
                    return;
                }

                if (request.Path == "/.well-known/site.standard.publication")
                {
                    wellKnownPublicationDocumentRequestCount++;
                    await response.WriteAsync(expectedStandardSitePublication.ToString());
                    return;
                }
            }
            else if (request.Host.Host == "plc.directory")
            {
                didDocumentRequestCount++;

                if (request.Path == $"/{expectedDid}")
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{expectedDid}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                            id : "#atproto_pds",
                            type : "atprotopds",
                            serviceEndpoint : TestServerBuilder.DefaultUri
                            )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
            }
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = expectedCredentials;

            Uri uri = new($"{TestServerBuilder.DefaultUri}document/12345");
            StandardSiteCardGenerator standardSiteCardGenerator = agent.CreateStandardSiteEmbeddedCardGenerator();

            Post post = new()
            {
                Text = $"Check out this document: {uri}"
            };

            EmbeddedExternal? card = await standardSiteCardGenerator.Generate(uri, TestContext.Current.CancellationToken);

            Assert.NotNull(card);

            Assert.Equal(expectedCardUrl, card.External.Uri.ToString());
            Assert.Equal(expectedCardTitle, card.External.Title);
            Assert.Equal(expectedCardDescription, card.External.Description);
            Assert.Equal(expectedCid, card.External.Thumbnail!.Reference!.Link);
            Assert.Equal("image/png", card.External.Thumbnail.MimeType);
            Assert.Equal(999, card.External.Thumbnail.Size);

            Assert.Null(card.External.AssociatedRefs);

            Assert.Equal(1, didDocumentRequestCount);
            Assert.Equal(0, wellKnownPublicationDocumentRequestCount);
        }
    }

    [Fact]
    public async Task FullSiteStandardPageMetadataWithDuplicatesAndBackingRecordsPicksFirstInstanceCreatesCorrectCard()
    {
        Did expectedDid = "did:plc:test";
        Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
        RecordKey expectedDocumentRKey = new("3mn4upg7a4z2h");
        RecordKey expectedPublicationRKey = new("3meddhkrg5z2p");

        AtUri expectedStandardSiteDocument = new($"at://{expectedDid}/site.standard.document/{expectedDocumentRKey}");
        AtUri expectedStandardSitePublication = new($"at://{expectedDid}/site.standard.publication/{expectedPublicationRKey}");

        const string expectedCardUrl = "https://example.com/test-page";
        const string expectedCardTitle = "Test Document";
        const string expectedCardDescription = "This is a test document for Open Graph metadata.";

        AccessCredentials expectedCredentials = new(
                service: TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                refreshToken: "refreshToken");

        int didDocumentRequestCount = 0;
        int wellKnownPublicationDocumentRequestCount = 0;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == TestServerBuilder.DefaultUri.Host)
            {
                if (request.Path == "/document/12345")
                {
                    await response.WriteAsync(@"<!DOCTYPE html>");
                    await response.WriteAsync(@"<html>");
                    await response.WriteAsync(@"<head>");
                    await response.WriteAsync($@"<title>{expectedCardTitle}</title>");
                    await response.WriteAsync($@"<meta property=""og:title"" content=""{expectedCardTitle}"" />");
                    await response.WriteAsync($@"<meta property=""og:description"" content=""{expectedCardDescription}"" />");
                    await response.WriteAsync($@"<meta property=""og:url"" content=""{expectedCardUrl}"" />");
                    await response.WriteAsync($@"<meta property=""og:image"" content=""{TestServerBuilder.DefaultUri}document/12345/image.png"" />");
                    await response.WriteAsync($@"<link rel=""site.standard.document"" href=""{expectedStandardSiteDocument}"" />");
                    await response.WriteAsync($@"<link rel=""site.standard.document"" href=""WRONG"" />");
                    await response.WriteAsync($@"<link rel=""site.standard.publication"" href=""{expectedStandardSitePublication}"" />");
                    await response.WriteAsync($@"<link rel=""site.standard.publication"" href=""WRONG"" />");
                    await response.WriteAsync(@"</head>");
                    await response.WriteAsync(@"<body>");
                    await response.WriteAsync($@"<h1>{expectedCardTitle}</h1>");
                    await response.WriteAsync(@"</body>");
                    await response.WriteAsync(@"</html>");
                    return;
                }

                if (request.Path == "/document/12345/image.png")
                {
                    await response.SendFileAsync("image.png");
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.uploadBlob")
                {
                    string json = "{\"blob\":{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"},\"mimeType\":\"image/png\",\"size\":999}}";

                    response.ContentType = "application/json";
                    await response.WriteAsync(json);
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.getRecord")
                {
                    if (request.Query["repo"].FirstOrDefault()!.Equals(expectedDid))
                    {
                        string collection = request.Query["collection"].FirstOrDefault() ?? string.Empty;
                        string rkey = request.Query["rkey"].FirstOrDefault() ?? string.Empty;

                        if (collection.Equals("site.standard.document") &&
                            rkey.Equals(expectedDocumentRKey.ToString()))
                        {
                            response.ContentType = "application/json";

                            string content = $"{{\"uri\":\"{expectedStandardSiteDocument}\",\"cid\":\"{expectedCid}\",\"value\":{{\"test\":true}}}}";
                            await response.WriteAsync(content);
                            return;
                        }
                        else if (collection.Equals("site.standard.publication") &&
                                 rkey.Equals(expectedPublicationRKey.ToString()))
                        {
                            response.ContentType = "application/json";
                            string content = $"{{\"uri\":\"{expectedStandardSitePublication}\",\"cid\":\"{expectedCid}\",\"value\":{{\"test\":true}}}}";
                            await response.WriteAsync(content);
                            return;
                        }
                    }
                    response.StatusCode = 404;
                    return;
                }

                if (request.Path == "/.well-known/site.standard.publication")
                {
                    wellKnownPublicationDocumentRequestCount++;
                    await response.WriteAsync(expectedStandardSitePublication.ToString());
                    return;
                }
            }
            else if (request.Host.Host == "plc.directory")
            {
                didDocumentRequestCount++;

                if (request.Path == $"/{expectedDid}")
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{expectedDid}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                            id : "#atproto_pds",
                            type : "atprotopds",
                            serviceEndpoint : TestServerBuilder.DefaultUri
                            )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
            }
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = expectedCredentials;

            Uri uri = new($"{TestServerBuilder.DefaultUri}document/12345");
            StandardSiteCardGenerator standardSiteCardGenerator = agent.CreateStandardSiteEmbeddedCardGenerator();

            Post post = new()
            {
                Text = $"Check out this document: {uri}"
            };

            EmbeddedExternal? card = await standardSiteCardGenerator.Generate(uri, TestContext.Current.CancellationToken);

            Assert.NotNull(card);

            Assert.Equal(expectedCardUrl, card.External.Uri.ToString());
            Assert.Equal(expectedCardTitle, card.External.Title);
            Assert.Equal(expectedCardDescription, card.External.Description);
            Assert.Equal(expectedCid, card.External.Thumbnail!.Reference!.Link);
            Assert.Equal("image/png", card.External.Thumbnail.MimeType);
            Assert.Equal(999, card.External.Thumbnail.Size);

            Assert.NotNull(card.External.AssociatedRefs);
            Assert.Equal(2, card.External.AssociatedRefs.Count);
            Assert.Contains(new StrongReference(expectedStandardSiteDocument, expectedCid), card.External.AssociatedRefs);
            Assert.Contains(new StrongReference(expectedStandardSitePublication, expectedCid), card.External.AssociatedRefs);

            Assert.Equal(1, didDocumentRequestCount);
            Assert.Equal(0, wellKnownPublicationDocumentRequestCount);
        }
    }

}
