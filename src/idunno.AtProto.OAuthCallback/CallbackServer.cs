// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mime;
using System.Net.Sockets;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto.OAuthCallback
{
    /// <summary>
    /// Implements a web server running on localhost which responds to OAuth return POSTs.
    /// </summary>
    public sealed class CallbackServer : IAsyncDisposable
    {
        private const int DefaultTimeout = 60 * 5; // 5 minutes

        private readonly ILogger<CallbackServer> _logger;
        private readonly TaskCompletionSource<string> _source = new ();

        private WebApplication? _listener;

        /// <summary>
        /// Creates a new instance of <see cref="CallbackServer"/>.
        /// </summary>
        /// <param name="port">The port to listen on</param>
        /// <param name="path">An optional path the host should respond on.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use when creating loggers.</param>
        public CallbackServer(int port, string? path = null, ILoggerFactory? loggerFactory = default)
        {
            ResponseStyleSheet = Resources.StyleSheet;
            SuccessTitle = Resources.SuccessTitle;
            SuccessBody = Resources.SuccessBody;

            LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = LoggerFactory.CreateLogger<CallbackServer>();

            path ??= string.Empty;

            if (path.StartsWith('/'))
            {
                path = path[1..];
            }

            Uri = new Uri($"http://{IPAddress.Loopback}:{port}/{path}");

            WebApplicationBuilder builder = WebApplication.CreateBuilder();

            if (loggerFactory is not null)
            {
                builder.Services.AddSingleton<ILoggerFactory>(loggerFactory);
            }

            builder.Services.AddHostFiltering(options =>
            {
                options.AllowedHosts = [IPAddress.Loopback.ToString()];
                options.AllowEmptyHosts = false;
            });

            _listener = builder.Build();
            _listener.Urls.Add($"http://{IPAddress.Loopback}:{port}");

            _listener.MapShortCircuit(404, "robots.txt", "favicon.ico");

            _listener.MapGet($"{path}", PullQueryString);
            _listener.MapPost($"{path}", MethodNotAllowed);

            _listener.MapFallback(BadRequest);

            Logger.ListeningOn(_logger, Uri);

            _listener.RunAsync();
        }

        /// <summary>
        /// Gets a configured logger factory from which to create loggers.
        /// </summary>
        public ILoggerFactory LoggerFactory { get; init; }

        /// <summary>
        /// Gets or sets any CSS rendered when a callback has happened.
        /// </summary>
        public string? ResponseStyleSheet { get; set; }

        /// <summary>
        /// Gets or sets the HTML rendered when a callback has happened.
        /// </summary>
        public string SuccessBody { get; set; }

        /// <summary>
        /// Gets or sets the page title used when a callback has happened.
        /// </summary>
        public string? SuccessTitle { get; set; }

        /// <summary>
        /// Gets the URI of the server.
        /// </summary>
        public Uri Uri { get; init; }

        /// <summary>
        /// Wait for the specified <paramref name="timeoutInSeconds"/> for an oauth callback.
        /// </summary>
        /// <param name="timeoutInSeconds">The amount of time to wait for the callback to complete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task<string> WaitForCallbackAsync(int timeoutInSeconds = DefaultTimeout, CancellationToken cancellationToken = default)
        {
            Logger.AwaitingCallback(_logger, timeoutInSeconds);

            if (_listener is not null)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(timeoutInSeconds * 1000, cancellationToken).ConfigureAwait(false);
                    _source.TrySetCanceled(cancellationToken);
                }, cancellationToken);
            }
            else
            {
                Logger.ListenerIsNull(_logger);
                _source.SetCanceled(cancellationToken);
            }

            return _source.Task;
        }

        /// <summary>
        /// Releases the underlying WebApplication.
        /// </summary>
        /// <returns>The task object representing the asynchronous operation.</returns>
        private async ValueTask DisposeAsyncCore()
        {
            if (_listener is not null)
            {
                await _listener.StopAsync().ConfigureAwait(false);
                await _listener.DisposeAsync().ConfigureAwait(false);
            }

            _listener = null;
        }

        /// <summary>
        /// Disposes the Callback object and its underlying WebApplication.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finds a port to start the callback server on.
        /// </summary>
        /// <returns>A port number that is free and can be used to bind the callback server to.</returns>
        public static int GetRandomUnusedPort()
        {
            using (var listener = new TcpListener(IPAddress.Loopback, 0))
            {
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
        }

        [SuppressMessage("Design", "CA1031: Do not catch general exception types", Justification = "Catch all error handling")]
        private async Task PullQueryString(HttpContext context)
        {
            if (context.Request.QueryString.HasValue)
            {
                Logger.ReceivedCallback(_logger);
                _source.TrySetResult(context.Request.QueryString.Value);
            }
            else
            {
                Logger.ReceivedCallbackWithNoQuerystring(_logger);
            }

            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = MediaTypeNames.Text.Html;
                await context.Response.WriteAsync("<html>").ConfigureAwait(false);
                await context.Response.WriteAsync("<head>").ConfigureAwait(false);
                if (!string.IsNullOrEmpty(SuccessTitle))
                {
                    await context.Response.WriteAsync(SuccessTitle).ConfigureAwait(false);
                }
                if (!string.IsNullOrEmpty(ResponseStyleSheet))
                {
                    await context.Response.WriteAsync(ResponseStyleSheet).ConfigureAwait(false);
                }
                await context.Response.WriteAsync("</head>").ConfigureAwait(false);
                await context.Response.WriteAsync("<body>").ConfigureAwait(false);
                await context.Response.WriteAsync(SuccessBody).ConfigureAwait(false);
                await context.Response.WriteAsync("</body>").ConfigureAwait(false);
                await context.Response.WriteAsync("</html>").ConfigureAwait(false);

                await context.Response.Body.FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = MediaTypeNames.Text.Html;
                await context.Response.WriteAsync("<h1>Invalid request.</h1>").ConfigureAwait(false);
                await context.Response.Body.FlushAsync().ConfigureAwait(false);
            }
        }

        private async Task BadRequest(HttpContext context)
        {
            Logger.BadRequest(_logger, context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = MediaTypeNames.Text.Html;
            await context.Response.WriteAsync("<h1>Invalid request.</h1>").ConfigureAwait(false);
            await context.Response.Body.FlushAsync().ConfigureAwait(false);
        }


        private async Task MethodNotAllowed(HttpContext context)
        {
            Logger.MethodNotAllowed(_logger, context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            context.Response.ContentType = MediaTypeNames.Text.Html;
            await context.Response.WriteAsync("<h1>Method Not Allowed.</h1>").ConfigureAwait(false);
            await context.Response.Body.FlushAsync().ConfigureAwait(false);
        }

    }
}
