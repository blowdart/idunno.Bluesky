// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Server.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test
{
    public class ServerTests
    {
        [Fact]
        public async Task DirectDescribeServerIsDeserializedCorrectly()
        {
            string domainName = TestServerBuilder.CreateRandomHostName();

            TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.DescribeServerEndpoint)
                {
                    response.StatusCode = 200;
                    var serverDescription = new ServerDescription(
                        did: $"did:web:{domainName}",
                        contact: new Contact($"test@{domainName}"),
                        links: new Links
                        {
                            PrivacyPolicy = new Uri($"https://{domainName}/privacy"),
                            TermsOfService = new Uri($"https://{domainName}/terms")
                        },
                        availableUserDomains: [domainName],
                        inviteCodeRequired: false,
                        phoneVerificationRequired: false);
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });

            AtProtoHttpResult<ServerDescription> response = await AtProtoServer.DescribeServer(
                service: new Uri($"https://{domainName}"),
                httpClient: testServer.CreateClient(),
                loggerFactory: null,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal($"did:web:{domainName}", response.Result.Did);
            Assert.NotNull(response.Result.Contact);
            Assert.Equal($"test@{domainName}", response.Result.Contact.Email);

            Assert.False(response.Result.InviteCodeRequired);

            Assert.Single(response.Result.AvailableUserDomains);
            Assert.Equal(response.Result.AvailableUserDomains[0], domainName);

            Assert.NotNull(response.Result.Links);
            Assert.Equal(new Uri($"https://{domainName}/privacy"), response.Result.Links.PrivacyPolicy);
            Assert.Equal(new Uri($"https://{domainName}/terms"), response.Result.Links.TermsOfService);
        }

        [Fact]
        public async Task AgentDescribeServerIsDeserializedCorrectly()
        {
            string domainName = TestServerBuilder.CreateRandomHostName();

            TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.DescribeServerEndpoint)
                {
                    response.StatusCode = 200;
                    var serverDescription = new ServerDescription(
                        did: $"did:web:{domainName}",
                        contact: new Contact($"test@{domainName}"),
                        links: new Links
                        {
                            PrivacyPolicy = new Uri($"https://{domainName}/privacy"),
                            TermsOfService = new Uri($"https://{domainName}/terms")
                        },
                        availableUserDomains: [domainName],
                        inviteCodeRequired: false,
                        phoneVerificationRequired: false);
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });

            using (var agent = new AtProtoAgent(new Uri($"https://{domainName}"), new TestHttpClientFactory(testServer)))
            {
                AtProtoHttpResult<ServerDescription> response = await agent.DescribeServer(new Uri($"https://{domainName}"), TestContext.Current.CancellationToken);

                Assert.True(response.Succeeded);

                Assert.Equal($"did:web:{domainName}", response.Result.Did);
                Assert.NotNull(response.Result.Contact);
                Assert.Equal($"test@{domainName}", response.Result.Contact.Email);

                Assert.False(response.Result.InviteCodeRequired);

                Assert.Single(response.Result.AvailableUserDomains);
                Assert.Equal(response.Result.AvailableUserDomains[0], domainName);

                Assert.NotNull(response.Result.Links);
                Assert.Equal(new Uri($"https://{domainName}/privacy"), response.Result.Links.PrivacyPolicy);
                Assert.Equal(new Uri($"https://{domainName}/terms"), response.Result.Links.TermsOfService);
            }
        }
    }
}
