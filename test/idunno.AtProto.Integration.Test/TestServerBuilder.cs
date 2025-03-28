// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using Microsoft.Extensions.Hosting;

namespace idunno.AtProto.Integration.Test
{
    internal class TestServerBuilder
    {
        public static string DefaultDomainName => "test.internal";

        public static Uri DefaultUri => new($"https://{DefaultDomainName}:443");

        public static TestServer CreateServer(
            Uri domain,
            Func<HttpContext, Task>? handler = null)
        {
            return CreateServer(domain.ToString(), handler);
        }

        public static TestServer CreateServer(
            string? domainName = null,
            Func<HttpContext, Task>? handler = null)
        {
            domainName ??= "*";

            IHost host = new HostBuilder()
                .ConfigureWebHost(builder =>
                    builder.UseTestServer()
                        .Configure(app =>
                        {
                            app.Use(async (context, next) =>
                            {
                                HttpRequest request = context.Request;
                                HttpResponse response = context.Response;

                                if (request.Path == "/404")
                                {
                                    response.StatusCode = 404;
                                }
                                else if (request.Path == "/401")
                                {
                                    response.StatusCode = 401;
                                }
                                else if (request.Path == "/403")
                                {
                                    response.StatusCode = 403;
                                }
                                else if (handler is not null)
                                {
                                    await handler(context);
                                }
                                else
                                {
                                    await next(context);
                                }
                            });
                        })
                        .UseUrls($"https://{domainName}:443")
                    ).Build();

            host.Start();
            return host.GetTestServer();
        }

        public static string CreateRandomHostName(int length = 16)
        {
            return RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz0123456789", length);
        }
    }

    internal class TestHttpClientFactory(TestServer testServer) : IHttpClientFactory
    {
        private readonly TestServer _testServer = testServer;

        public HttpClient CreateClient(string name)
        {
            return _testServer.CreateClient();
        }
    }
}
