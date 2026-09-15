// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mime;
using System.Net.Sockets;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto.OAuthCallback;

/// <summary>
/// Implements a web server running on localhost which responds to OAuth return POSTs.
/// </summary>
public sealed class CallbackServer : IAsyncDisposable
{
    private const int DefaultTimeout = 60 * 5; // 5 minutes

    private const int MaximumTimeout = 60 * 60 * 24; // 24 hours

    private const int MaximumPortNumber = 65535;

    private readonly ILogger<CallbackServer> _logger;

    // Continuations must not run inline on the Kestrel request thread which publishes the callback,
    // otherwise a blocking continuation in the waiting caller stalls the response the browser is
    // waiting on.
    private readonly TaskCompletionSource<string> _source = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly CancellationTokenSource _disposalCancellationSource = new();

#if NET9_0_OR_GREATER
    private readonly Lock _syncLock = new();
#else
    private readonly object _syncLock = new();
#endif

    private CancellationTokenSource? _timeoutCancellationSource;
    private CancellationTokenRegistration _timeoutRegistration;
    private CancellationToken _callerCancellationToken;
    private bool _disposed;

    private WebApplication? _listener;

    /// <summary>
    /// Creates a new instance of <see cref="CallbackServer"/>.
    /// </summary>
    /// <param name="port">The port to listen on</param>
    /// <param name="path">An optional path the host should respond on.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use when creating loggers.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="port"/> is zero or negative, or is greater than 65535.</exception>
    [SuppressMessage("Minor Vulnerability", "S5332:Clear-text protocols should not be used", Justification = "Has to be clear text, as local machines may not have a trusted localhost certificate and we shouldn't create one.")]
    public CallbackServer(int port, string? path = null, ILoggerFactory? loggerFactory = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, MaximumPortNumber);

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

        // This server receives OAuth authorization codes, so it must only ever be reachable from the
        // local machine. WebApplication.CreateBuilder() loads ambient configuration - appsettings.json
        // from the current working directory, environment variables and the command line - and a
        // Kestrel:Endpoints section in that configuration replaces any address configured through Urls,
        // which would silently move the listener onto an externally reachable interface while Uri still
        // reported loopback. Drop those sources and bind Kestrel explicitly so the hosting application's
        // configuration cannot influence where this server listens.
        builder.Configuration.Sources.Clear();

        builder.WebHost.ConfigureKestrel(kestrelOptions => kestrelOptions.Listen(IPAddress.Loopback, port));

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

        _listener.MapShortCircuit(404, "robots.txt", "favicon.ico");

        _listener.MapGet($"{path}", PullQueryString);
        _listener.MapPost($"{path}", MethodNotAllowed);

        _listener.MapFallback(BadRequest);

        Logger.ListeningOn(_logger, Uri);

        // RunAsync() faults if the server cannot start, for example when another process claimed the
        // port after GetRandomUnusedPort() released it. Leaving the task unobserved means the instance
        // looks constructed but is dead and every caller waiting for a callback hangs until it times
        // out, so surface the failure to the waiter instead.
        _ = _listener.RunAsync().ContinueWith(
            static (listenerTask, state) =>
            {
                CallbackServer server = (CallbackServer)state!;

                if (listenerTask.Exception is not null)
                {
                    Logger.ListenerFaulted(server._logger, listenerTask.Exception);
                    server._source.TrySetException(listenerTask.Exception.InnerExceptions);
                }
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutInSeconds"/> is zero or negative, or is greater than 86400.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the server has been disposed.</exception>
    /// <remarks>
    /// <para>A server only ever accepts a single callback. The timeout is armed by the first call, so
    /// later calls simply return the same task and their <paramref name="timeoutInSeconds"/> and
    /// <paramref name="cancellationToken"/> are ignored.</para>
    /// </remarks>
    public Task<string> WaitForCallbackAsync(int timeoutInSeconds = DefaultTimeout, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutInSeconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeoutInSeconds, MaximumTimeout);

        Logger.AwaitingCallback(_logger, timeoutInSeconds);

        lock (_syncLock)
        {
            if (_listener is null)
            {
                Logger.ListenerIsNull(_logger);
                _source.TrySetCanceled(cancellationToken);

                return _source.Task;
            }

            if (_timeoutRegistration != default)
            {
                Logger.CallbackAlreadyAwaited(_logger);

                return _source.Task;
            }

            // Registering on a linked source rather than awaiting Task.Delay means cancellation of
            // cancellationToken actually completes _source. Awaiting the delay would throw instead,
            // leaving the completion source pending forever and the caller hung.
            _callerCancellationToken = cancellationToken;
            _timeoutCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposalCancellationSource.Token);
            _timeoutRegistration = _timeoutCancellationSource.Token.Register(static state => ((CallbackServer)state!).CompleteAsCancelled(), this);
            _timeoutCancellationSource.CancelAfter(TimeSpan.FromSeconds(timeoutInSeconds));
        }

        return _source.Task;
    }

    /// <summary>
    /// Completes the pending callback task when the wait was cancelled, timed out, or the server was disposed.
    /// </summary>
    private void CompleteAsCancelled()
    {
        if (_disposalCancellationSource.IsCancellationRequested)
        {
            _source.TrySetException(new ObjectDisposedException(nameof(CallbackServer)));
        }
        else if (_callerCancellationToken.IsCancellationRequested)
        {
            _source.TrySetCanceled(_callerCancellationToken);
        }
        else
        {
            Logger.CallbackTimedOut(_logger);
            _source.TrySetCanceled(new CancellationToken(canceled: true));
        }
    }

    /// <summary>
    /// Releases the underlying WebApplication.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    private async ValueTask DisposeAsyncCore()
    {
        WebApplication? listener;

        lock (_syncLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Take the listener under the lock so a concurrent WaitForCallbackAsync either arms its
            // timeout against a live server or observes the null listener, never a half torn down one.
            listener = _listener;
            _listener = null;
        }

        // Unblock anyone still waiting on a callback rather than leaving them pending forever.
        await _disposalCancellationSource.CancelAsync().ConfigureAwait(false);
        CompleteAsCancelled();

        if (listener is not null)
        {
            await listener.StopAsync(CancellationToken.None).ConfigureAwait(false);
            await listener.DisposeAsync().ConfigureAwait(false);
        }

        await _timeoutRegistration.DisposeAsync().ConfigureAwait(false);
        _timeoutCancellationSource?.Dispose();
        _disposalCancellationSource.Dispose();
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
    /// <remarks>
    /// <para>The port is released before it is returned, so another process on the machine can claim it
    /// before the callback server binds to it. Construct the <see cref="CallbackServer"/> immediately
    /// after calling this to keep that window as small as possible. If the port is lost the task
    /// returned by <see cref="WaitForCallbackAsync(int, CancellationToken)"/> faults rather than
    /// hanging.</para>
    /// </remarks>
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
        // Kestrel is bound to the loopback adapter, so this should be unreachable. It is kept as defence
        // in depth because host filtering only inspects the Host header, which any client can forge.
        IPAddress? remoteAddress = context.Connection.RemoteIpAddress;

        if (remoteAddress is not null && !IPAddress.IsLoopback(remoteAddress))
        {
            Logger.NonLoopbackRequestRejected(_logger, remoteAddress);

            await BadRequest(context).ConfigureAwait(false);

            return;
        }

        string? queryString = null;

        if (context.Request.QueryString.HasValue)
        {
            Logger.ReceivedCallback(_logger);
            queryString = context.Request.QueryString.Value;
        }
        else
        {
            Logger.ReceivedCallbackWithNoQuerystring(_logger);
        }

        try
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = MediaTypeNames.Text.Html;
            await context.Response.WriteAsync("<html>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            await context.Response.WriteAsync("<head>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(SuccessTitle))
            {
                await context.Response.WriteAsync(SuccessTitle, cancellationToken: context.RequestAborted).ConfigureAwait(false);
            }
            if (!string.IsNullOrEmpty(ResponseStyleSheet))
            {
                await context.Response.WriteAsync(ResponseStyleSheet, cancellationToken: context.RequestAborted).ConfigureAwait(false);
            }
            await context.Response.WriteAsync("</head>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            await context.Response.WriteAsync("<body>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            await context.Response.WriteAsync(SuccessBody, cancellationToken: context.RequestAborted).ConfigureAwait(false);
            await context.Response.WriteAsync("</body>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            await context.Response.WriteAsync("</html>", cancellationToken: context.RequestAborted).ConfigureAwait(false);

            await context.Response.Body.FlushAsync(cancellationToken: context.RequestAborted).ConfigureAwait(false);
        }
        catch
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = MediaTypeNames.Text.Html;
            await context.Response.WriteAsync("<h1>Invalid request.</h1>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            await context.Response.Body.FlushAsync(cancellationToken: context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            // Publish only once the page has been written. Completing the task earlier hands control to
            // the waiting caller while this request is still in flight, so a slow continuation would
            // delay the response the browser is waiting on.
            if (queryString is not null)
            {
                _source.TrySetResult(queryString);
            }
        }
    }

    private async Task BadRequest(HttpContext context)
    {
        Logger.BadRequest(_logger, context.Request.Path);

        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        context.Response.ContentType = MediaTypeNames.Text.Html;
        await context.Response.WriteAsync("<h1>Invalid request.</h1>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(cancellationToken: context.RequestAborted).ConfigureAwait(false);
    }

    private async Task MethodNotAllowed(HttpContext context)
    {
        Logger.MethodNotAllowed(_logger, context.Request.Path);

        context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
        context.Response.ContentType = MediaTypeNames.Text.Html;
        await context.Response.WriteAsync("<h1>Method Not Allowed.</h1>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(cancellationToken: context.RequestAborted).ConfigureAwait(false);
    }
}