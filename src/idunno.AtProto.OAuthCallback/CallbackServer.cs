// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto.OAuthCallback;

/// <summary>
/// Implements a web server running on the loopback adapter which handles the redirect an OAuth
/// authorization server makes at the end of an authorization flow.
/// </summary>
/// <remarks>
/// <para>Only <c>GET</c> requests are accepted, as described in RFC 8252 section 7.3. Any other
/// method receives a <see cref="HttpStatusCode.MethodNotAllowed"/> response.</para>
/// <para>A request is only treated as a callback when it looks like the end of an authorization flow: it must carry a
/// query string containing at least one of <c>code</c>, <c>state</c> or <c>error</c>, and, if the requesting browser
/// says what the request was for, it must be a top level navigation. Anything else receives a
/// <see cref="HttpStatusCode.BadRequest"/> response and leaves the pending callback pending, so a page the user
/// happens to be visiting cannot consume the single callback this server accepts and deny the login.</para>
/// </remarks>
public sealed class CallbackServer : IAsyncDisposable
{
    private const int DefaultTimeout = 60 * 5; // 5 minutes

    private const int MaximumTimeout = 60 * 60 * 24; // 24 hours

    private const int MaximumPortNumber = 65535;

    internal const int MaximumCreationAttempts = 5;

    // Declaring the character set keeps the browser from sniffing an encoding for a page which may
    // contain a caller supplied SuccessBody.
    private const string HtmlContentType = "text/html; charset=utf-8";

    // The unreserved characters of RFC 3986 plus the segment separator. Anything else either means something to the
    // routing template parser or changes what the advertised Uri points at.
    private static readonly SearchValues<char> s_validPathCharacters = SearchValues.Create(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~/");

    // A browser performing the redirect at the end of an authorization flow navigates the top level document, so it
    // sends Sec-Fetch-Dest: document. A cross site request made by a page the user happens to be visiting carries
    // something else, such as image or empty.
    private const string NavigationFetchDestination = "document";

    private const string FetchDestinationHeader = "Sec-Fetch-Dest";

    // The default page carries an inline style sheet and a data uri image, and nothing else. Denying everything else
    // means a page whose URL is a secret cannot name a third party which could be told that URL.
    private const string DefaultContentSecurityPolicy =
        "default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'";

    private readonly ILogger<CallbackServer> _logger;

    // Continuations must not run inline on the Kestrel request thread which publishes the callback,
    // otherwise a blocking continuation in the waiting caller stalls the response the browser is
    // waiting on.
    private readonly TaskCompletionSource<string> _source = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Task _startupTask;

    private readonly CancellationTokenSource _disposalCancellationSource = new();
    private readonly Socket[] _ownedSockets;

#if NET9_0_OR_GREATER
    private readonly Lock _syncLock = new();
#else
    private readonly object _syncLock = new();
#endif

    private CancellationTokenSource? _timeoutCancellationSource;
    private CancellationTokenRegistration _timeoutRegistration;
    private CancellationToken _callerCancellationToken;
    private long _callbackWaitStartTimestamp;
    private bool _callbackAwaited;
    private volatile bool _disposed;

    private WebApplication? _listener;

    /// <summary>
    /// Creates a new instance of <see cref="CallbackServer"/>.
    /// </summary>
    /// <param name="port">The port to listen on</param>
    /// <param name="path">An optional path the host should respond on.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use when creating loggers.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="port"/> is zero or negative, or is greater than 65535.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> contains a character which is not valid in a path segment,
    /// or contains a dot segment which a <see cref="System.Uri"/> would resolve away.</exception>
    [SuppressMessage("Minor Vulnerability", "S5332:Clear-text protocols should not be used", Justification = "Has to be clear text, as local machines may not have a trusted localhost certificate and we shouldn't create one.")]
    public CallbackServer(int port, string? path = null, ILoggerFactory? loggerFactory = default)
        : this(port, path, loggerFactory, [], null, CancellationToken.None)
    {
    }

    [SuppressMessage("Minor Vulnerability", "S5332:Clear-text protocols should not be used", Justification = "Has to be clear text, as local machines may not have a trusted localhost certificate and we shouldn't create one.")]
    private CallbackServer(
        int port,
        string? path,
        ILoggerFactory? loggerFactory,
        Socket[] ownedSockets,
        Action<CallbackServer>? configure,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, MaximumPortNumber);

        _ownedSockets = ownedSockets;

        ResponseStyleSheet = Resources.StyleSheet;
        SuccessTitle = Resources.SuccessTitle;
        SuccessBody = Resources.SuccessBody;
        FailureTitle = Resources.FailureTitle;
        FailureBody = Resources.FailureBody;

        LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = LoggerFactory.CreateLogger<CallbackServer>();

        path ??= string.Empty;

        if (path.StartsWith('/'))
        {
            path = path[1..];
        }

        // The path goes into both the route pattern and the advertised Uri without escaping. A '{', '}' or '*' would
        // be read as a route parameter or a catch all rather than as literal text, and a '?', '#' or a space would
        // make the Uri a caller hands to an authorization server describe something other than the route which was
        // actually mapped. Restricting the path to characters which mean the same thing in both avoids a redirect URI
        // which cannot be matched, or a route which answers far more than it should.
        int invalidCharacterIndex = path.AsSpan().IndexOfAnyExcept(s_validPathCharacters);

        if (invalidCharacterIndex >= 0)
        {
            throw new ArgumentException($"'{path[invalidCharacterIndex]}' is not valid in a callback path.", nameof(path));
        }

        Uri uri = new($"http://{IPAddress.Loopback}:{port}/{path}");

        // A '.' or '..' segment is made of characters which are valid in a path, but a Uri resolves dot segments as it
        // is built, so the address a caller hands to an authorization server would point at a different route from the
        // one mapped below and the redirect would arrive to find nothing listening for it. Comparing the path back
        // against the route catches that, and anything else a Uri rewrites, rather than enumerating the cases.
        if (!string.Equals(uri.AbsolutePath, $"/{path}", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"'{path}' does not survive being resolved into a uri, so it cannot be used as a callback path.",
                nameof(path));
        }

        Uri = uri;

        _startupTask = InitializeListenerAsync(port, path, loggerFactory, ownedSockets, configure, cancellationToken);

        _ = RunListenerAsync().ContinueWith(
            static (listenerTask, state) =>
            {
                CallbackServer server = (CallbackServer)state!;

                if (listenerTask.Exception is not null)
                {
                    Logger.ListenerFaulted(server._logger, listenerTask.Exception);
                    if (server._source.TrySetException(listenerTask.Exception.InnerExceptions))
                    {
                        CallbackServerMetrics.RecordCallbackCompletion(
                            "listener_error", Interlocked.Read(ref server._callbackWaitStartTimestamp));
                        server.ReleaseTimeout();
                    }
                }
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        // Observe failures even when no caller waits for a callback.
        _ = _source.Task.ContinueWith(
            static faulted => _ = faulted.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        _ = _startupTask.ContinueWith(
            static faulted => _ = faulted.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task InitializeListenerAsync(
        int port,
        string path,
        ILoggerFactory? loggerFactory,
        Socket[] ownedSockets,
        Action<CallbackServer>? configure,
        CancellationToken cancellationToken)
    {
        try
        {
            configure?.Invoke(this);
            await BuildAndStartListenerAsync(port, path, loggerFactory, ownedSockets, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (_listener is not null)
            {
                await _listener.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    private async Task BuildAndStartListenerAsync(
        int port,
        string path,
        ILoggerFactory? loggerFactory,
        Socket[] ownedSockets,
        CancellationToken cancellationToken)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // This server receives OAuth authorization codes, so it must only ever be reachable from the
        // local machine. WebApplication.CreateBuilder() loads ambient configuration - appsettings.json
        // from the current working directory, environment variables and the command line - and a
        // Kestrel:Endpoints section in that configuration replaces any address configured through Urls,
        // which would silently move the listener onto an externally reachable interface while Uri still
        // reported loopback. Drop those sources and bind Kestrel explicitly so the hosting application's
        // configuration cannot influence where this server listens.
        builder.Configuration.Sources.Clear();

        builder.WebHost.ConfigureKestrel(kestrelOptions =>
        {
            if (ownedSockets.Length == 0)
            {
                kestrelOptions.Listen(IPAddress.Loopback, port);

                // RFC 8252 section 7.3 calls for both loopback families to be supported, because a redirect
                // URI written as http://localhost resolves to ::1 on many machines. The bind is conditional
                // so a machine with IPv6 disabled does not fail to start the server at all.
                if (Socket.OSSupportsIPv6)
                {
                    kestrelOptions.Listen(IPAddress.IPv6Loopback, port);
                }
            }
            else
            {
                foreach (Socket socket in ownedSockets)
                {
                    kestrelOptions.ListenHandle(unchecked((ulong)socket.Handle.ToInt64()));
                }
            }
        });

        if (loggerFactory is not null)
        {
            builder.Services.AddSingleton<ILoggerFactory>(loggerFactory);
        }

        builder.Services.AddHostFiltering(options =>
        {
            options.AllowedHosts = [IPAddress.Loopback.ToString(), "localhost", $"[{IPAddress.IPv6Loopback}]"];
            options.AllowEmptyHosts = false;
        });

        _listener = builder.Build();

        // The request this server answers carries the OAuth authorization code in its query string, so
        // the whole URL is a secret. Referrer-Policy stops it being handed to a third party through the
        // Referer header of any resource a caller supplied SuccessBody or ResponseStyleSheet references,
        // and Cache-Control keeps it out of the browser cache and any intermediary.
        _listener.Use(async (context, next) =>
        {
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";

            string? contentSecurityPolicy = ContentSecurityPolicy;

            if (!string.IsNullOrEmpty(contentSecurityPolicy))
            {
                context.Response.Headers.ContentSecurityPolicy = contentSecurityPolicy;
            }

            await next(context).ConfigureAwait(false);
        });

        _listener.MapShortCircuit(404, "robots.txt", "favicon.ico");

        _listener.MapGet($"{path}", PullQueryString);

        // Only GET is answered, so anything else is a method problem wherever it was addressed, and a fallback which
        // looks at the method keeps that true for verbs which are not mapped at all rather than only for the handful
        // which are.
        _listener.MapFallback(context => HttpMethods.IsGet(context.Request.Method) ? BadRequest(context) : MethodNotAllowed(context));

        Logger.ListeningOn(_logger, Uri);

        await _listener.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a callback server on an automatically allocated loopback port and waits for it to start.
    /// </summary>
    /// <param name="path">An optional path the host should respond on.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use when creating loggers.</param>
    /// <param name="configure">An optional action to configure the callback response before the listener starts.</param>
    /// <param name="cancellationToken">A token that can cancel server creation.</param>
    /// <returns>A started callback server whose <see cref="Uri"/> is ready to use.</returns>
    /// <exception cref="ArgumentException">The callback path contains an invalid character or a dot segment.</exception>
    /// <exception cref="OperationCanceledException">Server creation was cancelled.</exception>
    /// <exception cref="InvalidOperationException">The callback port could not be allocated after repeated address collisions.</exception>
    /// <remarks>
    /// <para>The server binds and retains its loopback sockets before handing them to Kestrel, so no other process can
    /// claim the selected port between allocation and startup. When IPv6 is supported, both loopback families use the
    /// same port. The returned server is ready to accept callbacks.</para>
    /// </remarks>
    public static Task<CallbackServer> CreateAsync(
        string? path = null,
        ILoggerFactory? loggerFactory = null,
        Action<CallbackServer>? configure = null,
        CancellationToken cancellationToken = default)
    {
        return CreateAsync(path, loggerFactory, configure, null, cancellationToken);
    }

    internal static async Task<CallbackServer> CreateAsync(
        string? path,
        ILoggerFactory? loggerFactory,
        Action<CallbackServer>? configure,
        Func<int>? getPort,
        CancellationToken cancellationToken)
    {
        List<int> attemptedPorts = [];

        for (int attempt = 1; attempt <= MaximumCreationAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Socket[] sockets = [];
            CallbackServer? server = null;
            int attemptedPort = getPort?.Invoke() ?? 0;
            bool allocatingSockets = true;

            try
            {
                sockets = BindLoopbackSockets(attemptedPort, out attemptedPort);
                attemptedPorts.Add(attemptedPort);
                allocatingSockets = false;

                server = new CallbackServer(attemptedPort, path, loggerFactory, sockets, configure, cancellationToken);
                sockets = [];

                await server._startupTask.ConfigureAwait(false);

                return server;
            }
            catch (Exception exception) when (allocatingSockets && IsAddressInUse(exception))
            {
                attemptedPorts.Add(attemptedPort);
                DisposeSockets(sockets);

                if (attempt == MaximumCreationAttempts)
                {
                    throw new InvalidOperationException(
                        $"Callback server startup failed after {MaximumCreationAttempts} address collisions. Attempted ports: {string.Join(", ", attemptedPorts)}.",
                        exception);
                }
            }
            catch
            {
                if (server is not null)
                {
                    await server.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    DisposeSockets(sockets);
                }

                throw;
            }
        }

        throw new InvalidOperationException("Callback server startup attempts were exhausted.");
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Socket ownership is transferred to the returned array or disposed when binding fails.")]
    private static Socket[] BindLoopbackSockets(int requestedPort, out int assignedPort)
    {
        assignedPort = requestedPort;
        Socket ipv4Socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Socket? ipv6Socket = null;

        try
        {
            ipv4Socket.Bind(new IPEndPoint(IPAddress.Loopback, requestedPort));
            assignedPort = ((IPEndPoint)ipv4Socket.LocalEndPoint!).Port;

            if (Socket.OSSupportsIPv6)
            {
                ipv6Socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                ipv6Socket.Bind(new IPEndPoint(IPAddress.IPv6Loopback, assignedPort));
            }

            return ipv6Socket is null ? [ipv4Socket] : [ipv4Socket, ipv6Socket];
        }
        catch
        {
            ipv6Socket?.Dispose();
            ipv4Socket.Dispose();

            throw;
        }
    }

    internal static bool IsAddressInUse(Exception exception)
    {
        return exception switch
        {
            AggregateException aggregate => aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(IsAddressInUse),
            SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse } => true,
            { InnerException: not null } => IsAddressInUse(exception.InnerException),
            _ => false
        };
    }

    private static void DisposeSockets(IEnumerable<Socket> sockets)
    {
        foreach (Socket socket in sockets)
        {
            socket.Dispose();
        }
    }

    /// <summary>
    /// Gets the task which completes when the listener has started, or faults with its startup exception.
    /// </summary>
    internal Task Startup => _startupTask;

    /// <summary>
    /// Runs the listener until shutdown, preserving the disposal performed by the host's RunAsync method.
    /// </summary>
    /// <returns>A task representing the listener lifetime.</returns>
    private async Task RunListenerAsync()
    {
        WebApplication? listener = _listener;
        await _startupTask.ConfigureAwait(false);

        if (listener is null)
        {
            throw new InvalidOperationException("Callback server startup completed without a listener.");
        }

        try
        {
            await listener.WaitForShutdownAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await listener.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets a configured logger factory from which to create loggers.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; init; }

    /// <summary>
    /// Gets or sets the <c>Content-Security-Policy</c> sent with every response, or <see langword="null" /> to send none.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The request this server answers carries the authorization code in its query string, so the address of the page
    ///   is a secret. The default policy allows only what the default page needs, which stops markup supplied through
    ///   <see cref="SuccessBody"/> or <see cref="FailureBody"/> reaching a third party which could be told that address.
    ///   A caller whose page loads anything else has to widen this to match.
    /// </para>
    /// </remarks>
    public string? ContentSecurityPolicy { get; set; } = DefaultContentSecurityPolicy;

    /// <summary>
    /// Gets or sets the CSS rendered when a callback has happened.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The value is written into the <c>head</c> of the page exactly as it is given, so it has to carry its own
    ///   <c>style</c> element. A bare style sheet would be rendered as text.
    /// </para>
    /// </remarks>
    public string? ResponseStyleSheet { get; set; }

    /// <summary>
    /// Gets or sets the HTML rendered in the body of the page when a callback carried an authorization code.
    /// </summary>
    /// <remarks>
    /// <para>The value is written into the page exactly as it is given, and is not encoded.</para>
    /// </remarks>
    public string SuccessBody { get; set; }

    /// <summary>
    /// Gets or sets the page title used when a callback carried an authorization code.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The value is written into the <c>head</c> of the page exactly as it is given, so it has to carry its own
    ///   <c>title</c> element. A bare title would be rendered as text at the top of the page.
    /// </para>
    /// </remarks>
    public string? SuccessTitle { get; set; }

    /// <summary>
    /// Gets or sets the HTML rendered in the body of the page when a callback did not carry an authorization code.
    /// </summary>
    /// <remarks>
    /// <para>The value is written into the page exactly as it is given, and is not encoded.</para>
    /// <para>When present, callback <c>error</c> and <c>error_description</c> values are HTML encoded and appended
    /// after this content in an <c>h2</c> heading and a paragraph, respectively. These values are untrusted server
    /// input and are displayed for information only; they do not establish the validity of the callback.</para>
    /// </remarks>
    public string FailureBody { get; set; }

    /// <summary>
    /// Gets or sets the page title used when a callback did not carry an authorization code.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The value is written into the <c>head</c> of the page exactly as it is given, so it has to carry its own
    ///   <c>title</c> element. A bare title would be rendered as text at the top of the page.
    /// </para>
    /// </remarks>
    public string? FailureTitle { get; set; }

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

        lock (_syncLock)
        {
            // The check above is not taken under the lock, so a disposal which ran between the two lands here. Reporting
            // it the same way keeps a caller from having to handle disposal as a cancellation as well as an exception
            // depending on which side of that window it arrived.
            if (_listener is null)
            {
                Logger.ListenerIsNull(_logger);

                throw new ObjectDisposedException(nameof(CallbackServer));
            }

            if (_callbackAwaited)
            {
                Logger.CallbackAlreadyAwaited(_logger);

                return _source.Task;
            }

            Logger.AwaitingCallback(_logger, timeoutInSeconds);

            // A dedicated flag rather than "_timeoutRegistration != default", because registering on a
            // token that is already cancelled runs the callback inline and hands back a default
            // registration, which would let a second call re-arm and leak the first timeout source.
            _callbackAwaited = true;
            Interlocked.Exchange(ref _callbackWaitStartTimestamp, Stopwatch.GetTimestamp());

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
    /// Releases the timeout armed by <see cref="WaitForCallbackAsync(int, CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The timeout is linked to the caller's cancellation token, so until it is released this instance, and the web
    ///   application it holds, stay reachable from that token. A caller which passes a token that lives as long as the
    ///   application would otherwise keep the server alive after the callback it was waiting for has arrived.
    /// </para>
    /// </remarks>
    private void ReleaseTimeout()
    {
        CancellationTokenRegistration registration;
        CancellationTokenSource? timeoutSource;

        lock (_syncLock)
        {
            // Disposal takes these apart itself, so leave them to it rather than racing it for them.
            if (_disposed)
            {
                return;
            }

            registration = _timeoutRegistration;
            _timeoutRegistration = default;

            timeoutSource = _timeoutCancellationSource;
            _timeoutCancellationSource = null;
        }

        registration.Dispose();
        timeoutSource?.Dispose();
    }

    /// <summary>
    /// Completes the pending callback task when the wait was cancelled, timed out, or the server was disposed.
    /// </summary>
    private void CompleteAsCancelled()
    {
        if (_disposalCancellationSource.IsCancellationRequested)
        {
            if (_source.TrySetException(new ObjectDisposedException(nameof(CallbackServer))))
            {
                long startTimestamp = Interlocked.Read(ref _callbackWaitStartTimestamp);
                if (startTimestamp != 0)
                {
                    CallbackServerMetrics.RecordCallbackCompletion("disposed", startTimestamp);
                }
            }
        }
        else if (_callerCancellationToken.IsCancellationRequested)
        {
            if (_source.TrySetCanceled(_callerCancellationToken))
            {
                long startTimestamp = Interlocked.Read(ref _callbackWaitStartTimestamp);
                if (startTimestamp != 0)
                {
                    CallbackServerMetrics.RecordCallbackCompletion("cancelled", startTimestamp);
                }
            }
        }
        else
        {
            Logger.CallbackTimedOut(_logger);
            if (_source.TrySetCanceled(new CancellationToken(canceled: true)))
            {
                CallbackServerMetrics.RecordCallbackCompletion("timeout", Interlocked.Read(ref _callbackWaitStartTimestamp));
            }
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

        try
        {
            if (listener is not null)
            {
                await listener.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                if (listener is not null)
                {
                    await listener.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                DisposeSockets(_ownedSockets);
            }
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
    /// <para>Prefer <see cref="CreateAsync(string?, ILoggerFactory?, Action{CallbackServer}?, CancellationToken)"/>,
    /// which keeps the allocated sockets bound and waits for the server to start. This method releases the port before
    /// returning it, so another process can claim it before a <see cref="CallbackServer"/> binds to it. If the port is
    /// lost, the task returned by <see cref="WaitForCallbackAsync(int, CancellationToken)"/> faults rather than hanging.</para>
    /// <para>A <see cref="CallbackServer"/> binds both loopback families, so a port which is free on IPv4 but taken on
    /// IPv6 is no use. The port returned is checked against both.</para>
    /// </remarks>
    public static int GetRandomUnusedPort()
    {
        const int maximumAttempts = 10;

        int port = 0;

        // Giving up after a number of attempts rather than looping forever, because a machine where every candidate
        // port is taken on IPv6 should fail when the server starts, with the exception that explains why, rather than
        // hang inside what looks like a trivial helper.
        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            using (var listener = new TcpListener(IPAddress.Loopback, 0))
            {
                listener.Start();
                port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
            }

            if (!Socket.OSSupportsIPv6 || IsFreeOnIPv6Loopback(port))
            {
                break;
            }
        }

        return port;
    }

    /// <summary>
    /// Returns whether <paramref name="port"/> can be bound on the IPv6 loopback adapter.
    /// </summary>
    /// <param name="port">The port to test.</param>
    /// <returns><see langword="true"/> if the port is free, otherwise <see langword="false"/>.</returns>
    private static bool IsFreeOnIPv6Loopback(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.IPv6Loopback, port);

            listener.Start();
            listener.Stop();

            return true;
        }
        catch (SocketException)
        {
            return false;
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

        // A page the user is visiting can point a browser at this server just as easily as the authorization server
        // can, and the Host it sends is the loopback address this server answers to, so neither host filtering nor the
        // remote address check above tells the two apart. The callback is single shot, so one cross site request would
        // otherwise consume it and the redirect which actually carries the authorization code would arrive to find the
        // wait already finished. Sec-Fetch-Dest separates them: the redirect at the end of an authorization flow is a
        // top level navigation, an <img> or a fetch from another page is not. The header is absent on browsers which
        // do not send it, and on non browser callers, so its absence is not treated as a rejection.
        string? fetchDestination = context.Request.Headers[FetchDestinationHeader];

        if (!string.IsNullOrEmpty(fetchDestination) &&
            !string.Equals(fetchDestination, NavigationFetchDestination, StringComparison.Ordinal))
        {
            Logger.NonNavigationRequestRejected(_logger, fetchDestination);

            await BadRequest(context).ConfigureAwait(false);

            return;
        }

        // Second half of the same defence, and the reason a bare request to the callback address no longer reports a
        // successful login. A request which carries none of the parameters an authorization server sends cannot be the
        // callback, so answering it as one would be wrong even if it were not a way to end the wait early.
        //
        // CodeQL reports cs/user-controlled-bypass against this condition. The branch it guards is the rejection, not
        // an authorization decision, so a request which satisfies the condition gains nothing beyond being passed to
        // the waiting caller. This server validates no part of the response it receives; the caller checks the OAuth
        // state parameter and the single use PKCE code verifier before any token exchange happens.
        if (!context.Request.QueryString.HasValue ||
            (!context.Request.Query.ContainsKey("code") &&
             !context.Request.Query.ContainsKey("state") &&
             !context.Request.Query.ContainsKey("error")))
        {
            Logger.ReceivedCallbackWithNoQuerystring(_logger);

            await BadRequest(context).ConfigureAwait(false);

            return;
        }

        Logger.ReceivedCallback(_logger);
        queryString = context.Request.QueryString.Value;

        // A callback which carries no authorization code did not complete a login, whatever else it carries, so telling
        // the person at the browser that it did would be wrong. The waiting caller is handed the query string either
        // way and decides what the failure was.
        bool authorizationCodeIssued = context.Request.Query.ContainsKey("code");

        string? title = authorizationCodeIssued ? SuccessTitle : FailureTitle;
        string body = authorizationCodeIssued ? SuccessBody : FailureBody;

        if (!authorizationCodeIssued)
        {
            Logger.ReceivedCallbackWithoutAnAuthorizationCode(_logger);
        }

        try
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = HtmlContentType;
            await context.Response.WriteAsync("<html>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            await context.Response.WriteAsync("<head>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(title))
            {
                await context.Response.WriteAsync(title, cancellationToken: context.RequestAborted).ConfigureAwait(false);
            }
            if (!string.IsNullOrEmpty(ResponseStyleSheet))
            {
                await context.Response.WriteAsync(ResponseStyleSheet, cancellationToken: context.RequestAborted).ConfigureAwait(false);
            }
            await context.Response.WriteAsync("</head>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            await context.Response.WriteAsync("<body>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            await context.Response.WriteAsync(body, cancellationToken: context.RequestAborted).ConfigureAwait(false);
            if (!authorizationCodeIssued)
            {
                string error = context.Request.Query["error"].ToString();
                string description = context.Request.Query["error_description"].ToString();
                bool hasErrorDetails = !string.IsNullOrEmpty(error) || !string.IsNullOrEmpty(description);
                if (hasErrorDetails)
                {
                    await context.Response.WriteAsync(
                        "<div class=\"oauth-error-details\">",
                        cancellationToken: context.RequestAborted).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(error))
                {
                    await context.Response.WriteAsync(
                        $"<h2>{WebUtility.HtmlEncode(error)}</h2>",
                        cancellationToken: context.RequestAborted).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(description))
                {
                    await context.Response.WriteAsync(
                        $"<p>{WebUtility.HtmlEncode(description)}</p>",
                        cancellationToken: context.RequestAborted).ConfigureAwait(false);
                }

                if (hasErrorDetails)
                {
                    await context.Response.WriteAsync("</div>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
                }
            }
            await context.Response.WriteAsync("</body>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            await context.Response.WriteAsync("</html>", cancellationToken: context.RequestAborted).ConfigureAwait(false);

            await context.Response.Body.FlushAsync(cancellationToken: context.RequestAborted).ConfigureAwait(false);
        }
        catch
        {
            // Setting the status code once the response has started throws, which would replace whatever went wrong
            // here with a less useful exception and skip the flush below. The browser has a partial page either way.
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = HtmlContentType;
                await context.Response.WriteAsync("<h1>Invalid request.</h1>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
            }

            await context.Response.Body.FlushAsync(cancellationToken: context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            // Publish only once the page has been written. Completing the task earlier hands control to
            // the waiting caller while this request is still in flight, so a slow continuation would
            // delay the response the browser is waiting on.
            if (queryString is not null)
            {
                if (_source.TrySetResult(queryString))
                {
                    CallbackServerMetrics.RecordCallbackCompletion(
                        authorizationCodeIssued ? "success" : "oauth_error",
                        Interlocked.Read(ref _callbackWaitStartTimestamp));
                }

                ReleaseTimeout();
            }
        }
    }

    private async Task BadRequest(HttpContext context)
    {
        Logger.BadRequest(_logger, context.Request.Path);
        CallbackServerMetrics.RecordRejectedRequest("bad_request");

        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        context.Response.ContentType = HtmlContentType;
        await context.Response.WriteAsync("<h1>Invalid request.</h1>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(cancellationToken: context.RequestAborted).ConfigureAwait(false);
    }

    private async Task MethodNotAllowed(HttpContext context)
    {
        Logger.MethodNotAllowed(_logger, context.Request.Method, context.Request.Path);
        CallbackServerMetrics.RecordRejectedRequest("method_not_allowed");

        context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
        context.Response.ContentType = HtmlContentType;
        await context.Response.WriteAsync("<h1>Method Not Allowed.</h1>", cancellationToken: context.RequestAborted).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(cancellationToken: context.RequestAborted).ConfigureAwait(false);
    }
}