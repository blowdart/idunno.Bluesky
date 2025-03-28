// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Threading.Tasks;
using idunno.AtProto.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static System.Formats.Asn1.AsnWriter;

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class BuilderTests
    {
        [Fact]
        public void BuilderWithServiceUriCreatesCorrectly()
        {
            var serviceUri = new Uri("https://test.com");

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(serviceUri);
            AtProtoAgent agent = builder.Build();

            Assert.Equal(serviceUri, builder.Service);
            Assert.Equal(serviceUri, agent.Service);
        }

        [Fact]
        public void BuilderWithServiceStringCreatesCorrectly()
        {
            const string service = "https://test.com";
            var serviceUri = new Uri(service);

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service);

            Assert.Equal(serviceUri, builder.Service);

            AtProtoAgent agent = builder.Build();

            Assert.Equal(serviceUri, builder.Service);
            Assert.Equal(serviceUri, agent.Service);
        }

        [Fact]
        public void BuilderSettingServicePropertyCreatesCorrectly()
        {
            const string service = "https://test.com";
            var serviceUri = new Uri(service);

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder();
            builder.Service = serviceUri;

            Assert.Equal(serviceUri, builder.Service);

            AtProtoAgent agent = builder.Build();

            Assert.Equal(serviceUri, builder.Service);
            Assert.Equal(serviceUri, agent.Service);
        }

        [Fact]
        public void BuilderWithDirectoryServiceUriCreatesCorrectly()
        {
            var serviceUri = new Uri("https://test.com");

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(serviceUri)
                .WithDirectoryService(serviceUri);

            Assert.Equal(serviceUri, builder.DirectoryService);

            AtProtoAgent agent = builder.Build();

            Assert.Equal(serviceUri, builder.DirectoryService);
            Assert.NotNull(agent._directoryAgent);
            Assert.Equal(serviceUri, agent._directoryAgent.PlcDirectory);
        }

        [Fact]
        public void BuilderWithDirectoryServicePropertyCreatesCorrectly()
        {
            var serviceUri = new Uri("https://test.com");
            var directoryServiceUri = new Uri("https://directory.test.com");

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(serviceUri);
            builder.DirectoryService = directoryServiceUri;

            Assert.Equal(directoryServiceUri, builder.DirectoryService);

            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent._directoryAgent);
            Assert.Equal(directoryServiceUri, agent._directoryAgent.PlcDirectory);
        }

        [Fact]
        public void BuilderWithDirectoryServiceStringCreatesCorrectly()
        {
            const string service = "https://test.com";
            var serviceUri = new Uri(service);

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service)
                .WithDirectoryService(serviceUri);
            AtProtoAgent agent = builder.Build();

            Assert.Equal(serviceUri, builder.Service);
            Assert.Equal(serviceUri, agent.Service);

            Assert.Equal(serviceUri, builder.DirectoryService);
            Assert.NotNull(agent._directoryAgent);
            Assert.Equal(serviceUri, agent._directoryAgent.PlcDirectory);
        }

        [Fact]
        public void BuilderSettingDirectoryServicePropertyCreatesCorrectly()
        {
            const string service = "https://test.com";
            var serviceUri = new Uri(service);

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service);
            builder.DirectoryService = serviceUri;

            AtProtoAgent agent = builder.Build();

            Assert.Equal(serviceUri, builder.Service);
            Assert.Equal(serviceUri, agent.Service);

            Assert.Equal(serviceUri, builder.DirectoryService);
            Assert.NotNull(agent._directoryAgent);
            Assert.Equal(serviceUri, agent._directoryAgent.PlcDirectory);
        }

        [Fact]
        public void SettingILoggerOnBuilderWorksCorrectly()
        {
            const string service = "https://test.com";

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service)
                .WithLoggerFactory(new LoggerFactory());
            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent.LoggerFactory);
            Assert.IsType<LoggerFactory>(agent.LoggerFactory);
        }

        [Fact]
        public void NotSettingALoggerUsesTheNullLoggerFactory()
        {
            const string service = "https://test.com";

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service);
            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent.LoggerFactory);
            Assert.IsType<NullLoggerFactory>(agent.LoggerFactory);
        }

        [Fact]
        public void NotSettingOAuthOptionsStillCreates()
        {
            const string service = "https://test.com";

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service);
            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);
        }

        [Fact]
        public void ConfiguringValidOAuthOptionsConfiguresCorrectly()
        {
            const string service = "https://test.com";
            var serviceUri = new Uri(service);

            const string clientId = "clientId";
            IEnumerable<string> scopes = ["atproto"];
            Uri returnUri = new ("https://return.invalid");

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service)
                .ConfigureOAuthOptions(options =>
                {
                    options.ClientId = clientId;
                    options.Scopes = scopes;
                    options.ReturnUri = returnUri;
                });
            
            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);
            Assert.NotNull(agent.Options);
            Assert.NotNull(agent.Options.OAuthOptions);
            Assert.Equal(clientId, agent.Options.OAuthOptions.ClientId);
            Assert.Equal(scopes, agent.Options.OAuthOptions.Scopes);
            Assert.Equal(returnUri, agent.Options.OAuthOptions.ReturnUri);
        }

        [Fact]
        public void SettingValidOAuthOptionsConfiguresCorrectly()
        {
            const string service = "https://test.com";

            const string clientId = "clientId";
            IEnumerable<string> scopes = ["atproto"];
            Uri returnUri = new("https://return.invalid");

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service);

            builder.OAuthOptions = new Authentication.OAuthOptions(
                    clientId,
                    returnUri,
                    ["atproto"]);

            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);
            Assert.NotNull(agent.Options);
            Assert.NotNull(agent.Options.OAuthOptions);
            Assert.Equal(clientId, agent.Options.OAuthOptions.ClientId);
            Assert.Equal(scopes, agent.Options.OAuthOptions.Scopes);
            Assert.Equal(returnUri, agent.Options.OAuthOptions.ReturnUri);
        }


        [Fact]
        public void MissingOAuthOptionsClientIdThrows()
        {
            const string service = "https://test.com";
            var serviceUri = new Uri(service);

            IEnumerable<string> scopes = ["atproto"];
            Uri returnUri = new("https://return.invalid");

            Assert.Throws<ArgumentNullException>(() =>
            {
                AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                    .ForService(service)
                    .ConfigureOAuthOptions(options =>
                    {
                        options.Scopes = scopes;
                        options.ReturnUri = returnUri;
                    });
            });
        }

        [Fact]
        public void ConfiguringOAuthOptionsClientIdToEmptyThrows()
        {
            const string service = "https://test.com";
            var serviceUri = new Uri(service);

            IEnumerable<string> scopes = ["atproto"];
            Uri returnUri = new("https://return.invalid");

            Assert.Throws<ArgumentException>(() =>
            {
                AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                    .ForService(service)
                    .ConfigureOAuthOptions(options =>
                    {
                        options.ClientId = string.Empty;
                        options.Scopes = scopes;
                        options.ReturnUri = returnUri;
                    });
            });
        }

        [Fact]
        public void LeavingOAuthOptionsClientIdNullThrows()
        {
            const string service = "https://test.com";
            var serviceUri = new Uri(service);

            Assert.Throws<ArgumentNullException>(() =>
            {
                AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                    .ForService(service);

                builder.OAuthOptions = new OAuthOptions();

                _ = builder.Build();

            });
        }

        [Fact]
        public void SettingOAuthOptionsClientIdToEmptyNullThrows()
        {
            const string service = "https://test.com";
            var serviceUri = new Uri(service);

            Assert.Throws<ArgumentNullException>(() =>
            {
                AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                    .ForService(service);

                OAuthOptions options = new()
                {
                    ClientId = string.Empty
                };

                builder.OAuthOptions = new OAuthOptions();

                _ = builder.Build();

            });
        }

        [Fact]
        public void ConfiguringHttpJsonOptionsSetsTheInternalJsonOptions()
        {
            const string service = "https://test.com";
            var serviceUri = new Uri(service);

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service)
                .ConfigureHttpJsonOptions(options =>
                {
                    options.JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true;
                });

            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);
            Assert.NotNull(agent.Options);
            Assert.NotNull(agent.JsonOptions);
            Assert.True(agent.JsonOptions.JsonSerializerOptions.AllowOutOfOrderMetadataProperties);
        }

        [Fact]
        public void SettingHttpJsonOptionsSetsTheInternalJsonOptions()
        {
            const string service = "https://test.com";
            var serviceUri = new Uri(service);

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service);

            var jsonOptions = new JsonOptions();
            jsonOptions.JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true;
            builder.JsonOptions = jsonOptions;

            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);
            Assert.NotNull(agent.Options);
            Assert.NotNull(agent.JsonOptions);
            Assert.True(agent.JsonOptions.JsonSerializerOptions.AllowOutOfOrderMetadataProperties);
        }

        [Fact]
        public void ConfiguringHttpClientOptionsSetsTheInternalHttpClientOptions()
        {
            const string service = "https://test.com";

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service)
                .ConfigureHttpClientOptions(options =>
                {
                    options.CheckCertificateRevocationList = false;
                });

            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);
            Assert.NotNull(agent.Options);
            Assert.NotNull(agent._httpClientOptions);
            Assert.False(agent._httpClientOptions.CheckCertificateRevocationList);
        }

        [Fact]
        public void SettingHttpClientOptionsSetsTheInternalHttpClientOptions()
        {
            const string service = "https://test.com";

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service);

            var httpClientOptions = new HttpClientOptions
            {
                CheckCertificateRevocationList = false
            };
            builder.HttpClientOptions = httpClientOptions;
            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);
            Assert.NotNull(agent.Options);
            Assert.NotNull(agent._httpClientOptions);
            Assert.False(agent._httpClientOptions.CheckCertificateRevocationList);
        }

        [Fact]
        public void ConfiguringDisableBackgroundTokenRefreshDisablesBackgroundTokenRefresh()
        {
            const string service = "https://test.com";

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service)
                .DisableBackgroundTokenRefresh();

            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);
            Assert.False(agent._enableTokenRefresh);
        }

        [Fact]
        public void ConfiguringEnableBackgroundTokenRefreshEnableBackgroundTokenRefresh()
        {
            const string service = "https://test.com";

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service)
                .EnableBackgroundTokenRefresh();

            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);
            Assert.True(agent._enableTokenRefresh);
        }

        [Fact]
        public void SettingBackgroundTokenRefreshEnabledToFalseDisablesBackgroundTokenRefresh()
        {
            const string service = "https://test.com";

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service);
            builder.BackgroundTokenRefreshEnabled = false;

            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);
            Assert.False(agent._enableTokenRefresh);
        }

        [Fact]
        public void BackgroundTokenRefreshIsEnabledByDefault()
        {
            const string service = "https://test.com";

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service);

            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);
            Assert.True(agent._enableTokenRefresh);
        }

        [Fact]
        public void ConfiguringAnHttpClientFactoryConfiguresTheFactoryInBothAgentAndDirectoryAgent()
        {
            const string service = "https://test.com";

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service)
                .WithHttpClientFactory(new HttpClientFactory());

            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);

            HttpClient httpClient = agent.HttpClient;

            Assert.Single(httpClient.DefaultRequestHeaders.UserAgent);

            ProductInfoHeaderValue userAgent = httpClient.DefaultRequestHeaders.UserAgent.First();
            Assert.NotNull(userAgent);
            Assert.NotNull(userAgent.Product);
            Assert.Equal("testclient", userAgent.Product.Name);

            HttpClient directoryHttpClient = agent._directoryAgent.HttpClient;

            Assert.Single(directoryHttpClient.DefaultRequestHeaders.UserAgent);

            ProductInfoHeaderValue directoryUserAgent = directoryHttpClient.DefaultRequestHeaders.UserAgent.First();
            Assert.NotNull(directoryUserAgent);
            Assert.NotNull(directoryUserAgent.Product);
            Assert.Equal("testclient", directoryUserAgent.Product.Name);
        }

        [Fact]
        public void SettingAnHttpClientFactoryConfiguresTheFactoryInBothAgentAndDirectoryAgent()
        {
            const string service = "https://test.com";

            AtProtoAgentBuilder builder = AtProtoAgent.CreateBuilder()
                .ForService(service);

            builder.HttpClientFactory = new HttpClientFactory();

            AtProtoAgent agent = builder.Build();

            Assert.NotNull(agent);

            HttpClient httpClient = agent.HttpClient;

            Assert.Single(httpClient.DefaultRequestHeaders.UserAgent);

            ProductInfoHeaderValue userAgent = httpClient.DefaultRequestHeaders.UserAgent.First();
            Assert.NotNull(userAgent);
            Assert.NotNull(userAgent.Product);
            Assert.Equal("testclient", userAgent.Product.Name);

            HttpClient directoryHttpClient = agent._directoryAgent.HttpClient;

            Assert.Single(directoryHttpClient.DefaultRequestHeaders.UserAgent);

            ProductInfoHeaderValue directoryUserAgent = directoryHttpClient.DefaultRequestHeaders.UserAgent.First();
            Assert.NotNull(directoryUserAgent);
            Assert.NotNull(directoryUserAgent.Product);
            Assert.Equal("testclient", directoryUserAgent.Product.Name);
        }

        class LoggerFactory : ILoggerFactory
        {
            public void AddProvider(ILoggerProvider provider) => throw new NotImplementedException();
            public ILogger CreateLogger(string categoryName) => new Logger();
            public void Dispose() => throw new NotImplementedException();
        }

        class Logger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => throw new NotImplementedException();
            public bool IsEnabled(LogLevel logLevel) => throw new NotImplementedException();
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => throw new NotImplementedException();
        }

        class HttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name)
            {
                HttpClient client = new();
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue("testclient")));
                return client;
            }
        }
    }
}
