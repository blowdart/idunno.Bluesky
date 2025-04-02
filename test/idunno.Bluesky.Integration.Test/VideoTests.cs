// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Video;
using idunno.AtProto.Server.Models;

namespace idunno.Bluesky.Integration.Test
{
    [ExcludeFromCodeCoverage]
    public class VideoTests
    {
        [Fact]
        public async Task VideoLogicRunsThroughFlowAsExpected()
        {
            Did expectedDid = "did:plc:test";
            string expectedJobId = "jobId";
            string expectedFilename = "test.mp4";
            string expectedLink = "bafkreiazmokgmpqpez2l4pyd2rleluqzw6tjef7hmj76ugsk53s6gh5l2i";

            string expectedServiceAuth = JwtBuilder.CreateJwt(
                did: null,
                issuer: expectedDid.ToString(),
                audience: "did:web:pds.test.internal",
                lxm: "com.atproto.repo.uploadBlob");

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            byte[] expectedBlob = Encoding.ASCII.GetBytes("expectedBlob");

            DateTimeOffset? serviceAuthTimeStamp = null;
            DateTimeOffset? uploadVideoTimeStamp = null;

            int jobStatusCallCount = 0;

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
                            did: $"{expectedDid}",
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

                    if (request.Path == "/xrpc/com.atproto.server.getServiceAuth")
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
                            serviceAuthTimeStamp = DateTimeOffset.UtcNow;

                            response.StatusCode = 200;
                            response.ContentType = "application/json";
                            await response.WriteAsync("{\n");
                            await response.WriteAsync("    \"token\": ");
                            await response.WriteAsync($"\"{expectedServiceAuth}\"\n");
                            await response.WriteAsync("}");
                            return;
                        }
                    }
                }

                if (request.Host.Host == "video.bsky.app")
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
                        request.Query["name"].ToString() == expectedFilename &&
                        request.Query["did"].Count == 1 &&
                        request.Query["did"].ToString() == expectedDid)
                    {
                        uploadVideoTimeStamp = DateTimeOffset.UtcNow;

                        if (request.Headers.Authorization.Count != 1)
                        {
                            response.StatusCode = 401;
                            return;
                        }
                        else if (request.Headers.Authorization.ToString() != $"Bearer {expectedServiceAuth}")
                        {
                            response.StatusCode = 403;
                            return;
                        }
                        else
                        {
                            byte[] body;

                            using (MemoryStream ms = new())
                            {
                                await request.Body.CopyToAsync(ms);
                                body = ms.ToArray();
                            }


                            if (!body.SequenceEqual(expectedBlob))
                            {
                                response.StatusCode = 500;
                                return;
                            }

                            response.StatusCode = 200;
                            response.ContentType= "application/json";

                            await response.WriteAsync("{");
                            await response.WriteAsync($"\"did\":\"did:plc:test\",");
                            await response.WriteAsync($"\"jobId\":\"jobId\",");
                            await response.WriteAsync($"\"state\":\"JOB_STATE_CREATED\"");
                            await response.WriteAsync("}");
                            return;
                        }
                    }

                    if (request.Path == "/xrpc/app.bsky.video.getJobStatus" &&
                        request.Query.Count != 0 &&
                        request.Query["jobId"].Count == 1 &&
                        request.Query["jobId"].ToString() == expectedJobId)
                    {
                        if (jobStatusCallCount == 0)
                        {
                            response.StatusCode = 200;
                            response.ContentType = "application/json";

                            await response.WriteAsync("{");
                            await response.WriteAsync("\"jobStatus\":");
                            await response.WriteAsync("{");
                            await response.WriteAsync($"\"did\":\"did:plc:test\",");
                            await response.WriteAsync($"\"jobId\":\"jobId\",");
                            await response.WriteAsync($"\"state\":\"JOB_STATE_CREATED\"");
                            await response.WriteAsync("}");
                            await response.WriteAsync("}");

                            jobStatusCallCount++;

                            return;
                        }

                        if (jobStatusCallCount == 1)
                        {
                            response.StatusCode = 200;
                            response.ContentType = "application/json";

                            await response.WriteAsync("{");
                            await response.WriteAsync("\"jobStatus\":");
                            await response.WriteAsync("{");
                            await response.WriteAsync($"\"did\":\"did:plc:test\",");
                            await response.WriteAsync($"\"jobId\":\"jobId\",");
                            await response.WriteAsync($"\"progress\":\"25\",");
                            await response.WriteAsync($"\"state\":\"JOB_STATE_ENCODING\"");
                            await response.WriteAsync("}");
                            await response.WriteAsync("}");

                            jobStatusCallCount++;

                            return;
                        }

                        if (jobStatusCallCount == 2)
                        {
                            response.StatusCode = 200;
                            response.ContentType = "application/json";

                            await response.WriteAsync("{");
                            await response.WriteAsync("\"jobStatus\":");
                            await response.WriteAsync("{");
                            await response.WriteAsync($"\"did\":\"did:plc:test\",");
                            await response.WriteAsync($"\"jobId\":\"jobId\",");
                            await response.WriteAsync($"\"state\":\"JOB_STATE_SCANNING\"");
                            await response.WriteAsync("}");
                            await response.WriteAsync("}");

                            jobStatusCallCount++;

                            return;
                        }

                        if (jobStatusCallCount == 3)
                        {
                            response.StatusCode = 200;
                            response.ContentType = "application/json";

                            await response.WriteAsync("{");
                            await response.WriteAsync("\"jobStatus\":");
                            await response.WriteAsync("{");
                            await response.WriteAsync($"\"did\":\"did:plc:test\",");
                            await response.WriteAsync($"\"jobId\":\"jobId\",");
                            await response.WriteAsync($"\"state\":\"JOB_STATE_SCANNED\"");
                            await response.WriteAsync("}");
                            await response.WriteAsync("}");

                            jobStatusCallCount++;

                            return;
                        }

                        if (jobStatusCallCount == 4)
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
                            await response.WriteAsync($"\"$link\":\"bafkreiazmokgmpqpez2l4pyd2rleluqzw6tjef7hmj76ugsk53s6gh5l2i\"");
                            await response.WriteAsync("},");
                            await response.WriteAsync($"\"mimeType\":\"video/mp4\",");
                            await response.WriteAsync($"\"size\":\"1024\"");
                            await response.WriteAsync("},");
                            await response.WriteAsync($"\"did\":\"did:plc:test\",");
                            await response.WriteAsync($"\"jobId\":\"jobId\",");
                            await response.WriteAsync($"\"message\":\"Video processed successfully\",");
                            await response.WriteAsync($"\"state\":\"JOB_STATE_COMPLETED\"");
                            await response.WriteAsync("}");
                            await response.WriteAsync("}");

                            jobStatusCallCount++;

                            return;
                        }
                    }
                }
            });

            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
            {
                int statusRetryCount = 0;
                int maxStatusRetries = 5;

                agent.Credentials = expectedCredentials;
                agent.Service = TestServerBuilder.DefaultUri;

                AtProtoHttpResult<JobStatus> uploadResult = await agent.UploadVideo(expectedFilename, expectedBlob, TestContext.Current.CancellationToken);

                Assert.True(uploadResult.Succeeded);
                Assert.Equal(JobState.Created, uploadResult.Result.State);
                Assert.Equal(expectedJobId, uploadResult.Result.JobId);
                Assert.Equal(expectedDid, uploadResult.Result.Did);

                // Wait for processing to finish.
                while (uploadResult.Succeeded &&
                       (uploadResult.Result.State == JobState.Created || uploadResult.Result.State == JobState.InProgress) &&
                       statusRetryCount < maxStatusRetries)
                {
                    statusRetryCount++;
                    uploadResult = await agent.GetVideoJobStatus(uploadResult.Result.JobId, TestContext.Current.CancellationToken);
                }

                Assert.True(uploadResult.Succeeded);
                Assert.Equal(JobState.Completed, uploadResult.Result.State);
                Assert.Equal(expectedJobId, uploadResult.Result.JobId);
                Assert.Equal(expectedDid, uploadResult.Result.Did);
                Assert.NotNull(uploadResult.Result.Blob);
                Assert.Equal(expectedLink, uploadResult.Result.Blob.Reference.Link);
            }


            Assert.NotNull(serviceAuthTimeStamp);
            Assert.NotNull(uploadVideoTimeStamp);

            Assert.True(serviceAuthTimeStamp < uploadVideoTimeStamp);
        }
    }
}
