// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mime;
using System.Net.Sockets;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto.OAuth
{
    /// <summary>
    /// Implements a web server running on localhost which responds to OAuth return POSTs.
    /// </summary>
    public class CallbackServer : IDisposable, IAsyncDisposable
    {
        private const int DefaultTimeout = 60 * 5; // 5 minutes

        private volatile bool _isDisposed;

        private readonly TaskCompletionSource<string> _source = new ();

        private WebApplication? _listener;

        private readonly ILogger<CallbackServer> _logger;

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

            builder.Services.AddHostFiltering(options =>
            {
                options.AllowedHosts = new List<string> { IPAddress.Loopback.ToString() };
                options.AllowEmptyHosts = false;
            });

            _listener = builder.Build();
            _listener.Urls.Add($"http://{IPAddress.Loopback}:{port}");

            _listener.MapGet($"{path}", PullQueryString);
            _listener.MapPost($"{path}", MethodNotAllowed);

            _listener.MapShortCircuit(404, "robots.txt", "favicon.ico");
            _listener.MapFallback(BadRequest);

            Logger.ListeningOn(_logger, Uri);

            _listener.RunAsync();
        }

        /// <summary>
        /// Gets a configured logger factory from which to create loggers.
        /// </summary>
        protected ILoggerFactory LoggerFactory { get; init; }

        /// <summary>
        /// Gets or sets any CSS rendered when a callback has happened.
        /// </summary>
        protected string? ResponseStyleSheet { get; set; }

        /// <summary>
        /// Gets or sets the HTML rendered when a callback has happened.
        /// </summary>
        protected string SuccessBody { get; set; }

        /// <summary>
        /// Gets or sets the page title used when a callback has happened.
        /// </summary>
        protected string? SuccessTitle { get; set; }

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
        /// Releases the unmanaged resources used by the <see cref="CallbackServer"/> and optionally disposes of the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to releases only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Releases the underlying WebApplication.
        /// </summary>
        /// <returns><see cref="ValueTask"/></returns>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_listener is not null)
            {
                await _listener.StopAsync().ConfigureAwait(false);
                await _listener.DisposeAsync().ConfigureAwait(false);
            }

            _listener = null;
        }

        /// <summary>
        /// Releases the resources held by this Callback object.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finds a port to start the callback server on.
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Disposes the Callback object and its underlying WebApplication.
        /// </summary>
        /// <returns><see cref="ValueTask"/></returns>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);

            Dispose(disposing: false);
            GC.SuppressFinalize(this);
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
