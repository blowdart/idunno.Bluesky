// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using idunno.AtProto.Jetstream;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class AtProtoJetstreamConnectionTests
{
    private const string TestDid = "did:plc:g6ylltenitt4tp27bpwalh7b";

    [Fact]
    public async Task AStateChangedHandlerDoesNotRunUnderTheLockWhichGuardsTheFilters()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, connectionNumber, serverCancellationToken) =>
        {
            if (connectionNumber == 1)
            {
                // Closed by the server so the socket reaches Closed, which is what makes the next connect replace it,
                // which is the path which used to raise the event from inside the lock.
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server disconnect", serverCancellationToken);
            }
        });

        using var handlerRunning = new ManualResetEventSlim(false);
        using var releaseHandler = new ManualResetEventSlim(false);

        bool armed = false;
        bool filterSetterBlocked;

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false }))
        {
            jetstream.ConnectionStateChanged += (sender, e) =>
            {
                if (Volatile.Read(ref armed))
                {
                    handlerRunning.Set();
                    releaseHandler.Wait(TimeSpan.FromSeconds(10));
                }
            };

            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                while (jetstream.State != WebSocketState.Closed && jetstream.State != WebSocketState.Aborted)
                {
                    await Task.Delay(50, cancellationToken);
                }

                Volatile.Write(ref armed, true);

                Task reconnect = Task.Run(
                    async () => await jetstream.ConnectAsync(
                        uri: server.Uri,
                        cursor: null,
                        httpClient: httpClient,
                        cancellationToken: cancellationToken),
                    cancellationToken);

                Assert.True(handlerRunning.Wait(TimeSpan.FromSeconds(10), cancellationToken), "The state changed handler never ran.");

                Task setter = Task.Run(
                    () => jetstream.DidFilter = [new Did(TestDid)],
                    cancellationToken);

                Task timeout = Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

                filterSetterBlocked = await Task.WhenAny(setter, timeout) == timeout;

                Volatile.Write(ref armed, false);
                releaseHandler.Set();

                await reconnect.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                await setter.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }

        // A handler which runs under the lock the filters are read and written under deadlocks against any thread which
        // holds a lock of its own and is setting a filter.
        Assert.False(filterSetterBlocked, "The DidFilter setter was blocked by a user event handler.");
    }

    [Fact]
    public async Task ReconnectingFromAStateChangedHandlerDoesNotBlock()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false }))
        {
            using (var httpClient = new HttpClient())
            {
                int handlerRuns = 0;
                long reentrantConnectMilliseconds = -1;

                jetstream.ConnectionStateChanged += (sender, e) =>
                {
                    if (Interlocked.Increment(ref handlerRuns) == 1)
                    {
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                        _ = jetstream.ConnectAsync(
                            uri: server.Uri,
                            cursor: null,
                            httpClient: httpClient,
                            cancellationToken: cancellationToken).Wait(TimeSpan.FromSeconds(10));

                        stopwatch.Stop();
                        reentrantConnectMilliseconds = stopwatch.ElapsedMilliseconds;
                    }
                };

                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

                // Reconnecting in response to a connection state change is the obvious thing for a handler to do, and
                // the semaphore which serialises connections is not reentrant, so a handler raised whilst it is held
                // waits on a semaphore its own caller holds.
                Assert.True(
                    reentrantConnectMilliseconds >= 0 && reentrantConnectMilliseconds < 2000,
                    $"A connect made from a state changed handler took {reentrantConnectMilliseconds}ms.");
            }
        }
    }

    [Fact]
    public async Task AServerWhichNeverAnswersACloseIsAbandonedOnceTheCloseTimeoutExpires()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer
        {
            // The close handshake only completes when the server answers it, and this one never reads the socket, so
            // it never will.
            DrainSockets = false
        };

        await server.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions
            {
                UseCompression = false,
                CloseTimeout = TimeSpan.FromSeconds(2)
            }))
        {
            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                Assert.True(jetstream.IsConnected);

                Task close = jetstream.CloseAsync(cancellationToken: cancellationToken);

                await close.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);

                Assert.Equal(WebSocketState.Aborted, jetstream.State);
                Assert.False(jetstream.DisconnectedGracefully);
            }
        }
    }

    [Fact]
    public async Task AConnectionWhichCannotBeMadeThrows()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Nothing is listening on the port, so the connection cannot be made.
        Uri deadUri = new(string.Create(CultureInfo.InvariantCulture, $"ws://localhost:{FreePort()}"));

        using (var jetstream = new AtProtoJetstream(
            uri: deadUri,
            options: new JetstreamOptions { UseCompression = false }))
        {
            using (var httpClient = new HttpClient())
            {
                // Returning normally from a connection which did not happen leaves a caller with no events, no error,
                // and nothing to say which of the two it is looking at.
                await Assert.ThrowsAsync<WebSocketException>(
                    async () => await jetstream.ConnectAsync(
                        uri: deadUri,
                        cursor: null,
                        httpClient: httpClient,
                        cancellationToken: cancellationToken));
            }
        }
    }

    [Fact]
    public async Task ConnectionMetricsAreNotTaggedWithTheSubscriptionFilters()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Did watchedDid = new("did:plc:g6ylltenitt4tp27bpwalh7b");
        Nsid watchedCollection = new("app.bsky.feed.post");

        using var server = new TestJetstreamServer();

        await server.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        using var meterFactory = new TestMeterFactory();

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false, MeterFactory = meterFactory },
            collections: [watchedCollection],
            dids: [watchedDid]))
        {
            using var collector = new MetricCollector<long>(meterFactory, "idunno.AtProto.Jetstream", "idunno.atproto.jetstream.total.connections_opened");

            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);
            }

            string?[] tags = [.. collector.GetMeasurementSnapshot().Select(measurement => measurement.Tags["server"]?.ToString())];

            Assert.NotEmpty(tags);

            // The subscription uri names every did and collection being watched. Tagging a metric with it publishes
            // who is being watched to whatever collects the metrics, and gives the tag an unbounded set of values.
            Assert.All(tags, tag => Assert.Equal(server.Uri.ToString(), tag));
            Assert.All(tags, tag => Assert.DoesNotContain("wantedDids", tag, StringComparison.Ordinal));
            Assert.All(tags, tag => Assert.DoesNotContain(watchedDid.ToString(), tag, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task AConnectionWhichCannotBeMadeIsCountedOnce()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Uri deadUri = new(string.Create(CultureInfo.InvariantCulture, $"ws://localhost:{FreePort()}"));

        using var meterFactory = new TestMeterFactory();

        using (var jetstream = new AtProtoJetstream(
            uri: deadUri,
            options: new JetstreamOptions { UseCompression = false, MeterFactory = meterFactory }))
        {
            using var collector = new MetricCollector<long>(meterFactory, "idunno.AtProto.Jetstream", "idunno.atproto.jetstream.total.connections_failed");

            using (var httpClient = new HttpClient())
            {
                await Assert.ThrowsAsync<WebSocketException>(
                    async () => await jetstream.ConnectAsync(
                        uri: deadUri,
                        cursor: null,
                        httpClient: httpClient,
                        cancellationToken: cancellationToken));
            }

            Assert.Equal(1, collector.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        }
    }

    [Fact]
    public async Task AMessageWhichCannotBeParsedIsTruncatedBeforeItIsLogged()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Long enough to be truncated, and not valid json, so it reaches the log rather than a handler.
        string oversizedGarbage = new('a', 64 * 1024);

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, connectionNumber, serverCancellationToken) =>
        {
            await webSocket.SendAsync(
                Encoding.UTF8.GetBytes(oversizedGarbage),
                WebSocketMessageType.Text,
                endOfMessage: true,
                serverCancellationToken);
        });

        using var provider = new FakeLoggerProvider();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(provider);
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions
            {
                UseCompression = false,
                LoggerFactory = loggerFactory,
                MaxMessageSize = 1024 * 1024
            }))
        {
            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                FakeLogRecord? parseFailure = null;

                for (int attempt = 0; attempt < 100 && parseFailure is null; attempt++)
                {
                    await Task.Delay(100, cancellationToken);

                    parseFailure = provider.Collector.GetSnapshot()
                        .FirstOrDefault(record => record.Message.Contains("could not parse", StringComparison.OrdinalIgnoreCase));
                }

                Assert.NotNull(parseFailure);

                // A message is remote input which can be as large as the maximum message size allows, so logging one
                // whole lets the server decide how much is written to the log.
                Assert.DoesNotContain(oversizedGarbage, parseFailure.Message, StringComparison.Ordinal);
                Assert.Contains("truncated", parseFailure.Message, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task DisposingDuringAConnectionAttemptDoesNotThrowFromTheConnectionSemaphore()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer
        {
            // Holds the connection attempt open long enough for the dispose to land in the middle of it.
            AcceptDelay = TimeSpan.FromSeconds(4)
        };

        await server.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        using var httpClient = new HttpClient();

        var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false });

        Task connect = Task.Run(
            async () => await jetstream.ConnectAsync(
                uri: server.Uri,
                cursor: null,
                httpClient: httpClient,
                cancellationToken: cancellationToken),
            cancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        jetstream.Dispose();

        Exception? thrown = await Record.ExceptionAsync(async () => await connect.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));

        // Disposing whilst a connection is in flight is expected to fail that connection, but it must fail because the
        // jetstream was disposed, not because the semaphore serialising connections was disposed underneath a caller
        // which was about to release it.
        if (thrown is ObjectDisposedException objectDisposedException)
        {
            Assert.DoesNotContain("Semaphore", objectDisposedException.ObjectName, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AConnectionLostWithoutACloseHandshakeRaisesAConnectionStateChange()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new RawJetstreamServer();

        await server.Start(async (client, connectionNumber, serverCancellationToken) =>
        {
            // Given long enough to let the client finish connecting and start reading, so the connection is lost rather
            // than never established.
            await Task.Delay(500, serverCancellationToken);

            RawJetstreamServer.Drop(client);
        });

        using var disconnected = new ManualResetEventSlim(false);

        WebSocketState? disconnectedState = null;

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false }))
        {
            jetstream.ConnectionStateChanged += (sender, e) =>
            {
                if (e.State is not WebSocketState.None and not WebSocketState.Open and not WebSocketState.Connecting)
                {
                    disconnectedState = e.State;
                    disconnected.Set();
                }
            };

            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                // A dropped connection is only visible to a consumer through this event, so a receive loop which ends
                // without raising it leaves anything waiting to reconnect waiting forever.
                Assert.True(
                    disconnected.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                    "No connection state change was raised when the connection was dropped.");
            }
        }

        Assert.NotNull(disconnectedState);
        Assert.NotEqual(WebSocketState.Open, disconnectedState!.Value);
    }

    [Fact]
    public async Task ReconnectingFromAFaultHandlerDoesNotLeaveTheOldReceiveLoopReadingTheNewSocket()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new RawJetstreamServer();

        // Large enough that it has to be reassembled from several reads, so two loops sharing the socket tear it apart
        // between them rather than taking one whole message each.
        string payload = "{\"did\":\"" + TestDid + "\",\"time_us\":1,\"kind\":\"identity\",\"pad\":\"" + new string('x', 8192) + "\"}";
        byte[] payloadAsBytes = Encoding.UTF8.GetBytes(payload);

        await server.Start(async (client, connectionNumber, serverCancellationToken) =>
        {
            if (connectionNumber == 1)
            {
                await Task.Delay(500, serverCancellationToken);

                RawJetstreamServer.Drop(client);
            }
            else
            {
                for (int sent = 0; sent < 200 && !serverCancellationToken.IsCancellationRequested; sent++)
                {
                    await RawJetstreamServer.SendTextFrame(client, payloadAsBytes, serverCancellationToken);
                }

                await Task.Delay(Timeout.Infinite, serverCancellationToken);
            }
        });

        List<string> faults = [];
        int faultCount = 0;
        int messagesReceived = 0;
        int tornMessages = 0;

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions
            {
                UseCompression = false,
                BufferSize = 512
            }))
        {
            using (var httpClient = new HttpClient())
            {
                jetstream.MessageReceived += (sender, e) =>
                {
                    Interlocked.Increment(ref messagesReceived);

                    if (!string.Equals(e.Message, payload, StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref tornMessages);
                    }
                };

                jetstream.FaultRaised += (sender, e) =>
                {
                    lock (faults)
                    {
                        faults.Add(e.Fault);
                    }

                    // Raised synchronously on the thread running the receive loop, so reconnecting here and waiting for
                    // it to finish puts a freshly connected socket in place before the loop takes its next turn. A loop
                    // which reads the field rather than the socket it was started for then reads that new socket
                    // alongside the loop which was started for it.
                    if (Interlocked.Increment(ref faultCount) == 1)
                    {
                        jetstream.ConnectAsync(
                            uri: server.Uri,
                            cursor: null,
                            httpClient: httpClient,
                            cancellationToken: cancellationToken).GetAwaiter().GetResult();
                    }
                };

                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        string[] recorded;

        lock (faults)
        {
            recorded = [.. faults];
        }

        Assert.True(recorded.Length > 0, "The dropped connection never raised a fault.");
        Assert.True(Volatile.Read(ref messagesReceived) > 0, "No messages were received after reconnecting.");

        // Two loops reading one socket take alternate reads of the same message, so each of them reassembles a mixture
        // of its own fragments and the other one's.
        Assert.Equal(0, Volatile.Read(ref tornMessages));
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// A WebSocket server built directly on a TCP socket, so a connection can be dropped with a reset rather than
    /// closed. <see cref="WebSocket.Abort"/> on an <see cref="HttpListener"/> socket does not reach the client on
    /// every platform, and a dropped connection is the thing these tests are about.
    /// </summary>
    private sealed class RawJetstreamServer : IDisposable
    {
        // Bound to port zero so the port is chosen as the listener binds. Choosing one up front and binding it later
        // leaves a window in which another listener, including one in a test process for another target framework,
        // can take it.
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private int _connectionCount;

        public Uri Uri { get; private set; } = new("ws://127.0.0.1");

        public Task Start(Func<TcpClient, int, CancellationToken, Task> onConnected)
        {
            _listener.Start();

            Uri = new Uri(
                string.Create(CultureInfo.InvariantCulture, $"ws://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}"));

            _ = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);

                    int connectionNumber = Interlocked.Increment(ref _connectionCount);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await CompleteHandshake(client, _cancellationTokenSource.Token);
                            await onConnected(client, connectionNumber, _cancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (IOException)
                        {
                        }
                        catch (SocketException)
                        {
                        }
                    }, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Drops the connection with a reset, which is what a connection lost to the network looks like to the client.
        /// </summary>
        public static void Drop(TcpClient client)
        {
            client.LingerState = new LingerOption(enable: true, seconds: 0);
            client.Close();
        }

        public static async Task SendTextFrame(TcpClient client, byte[] payload, CancellationToken cancellationToken)
        {
            byte[] header;

            if (payload.Length < 126)
            {
                header = [0x81, (byte)payload.Length];
            }
            else
            {
                header = [0x81, 126, (byte)(payload.Length >> 8), (byte)(payload.Length & 0xFF)];
            }

            NetworkStream stream = client.GetStream();

            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static async Task CompleteHandshake(TcpClient client, CancellationToken cancellationToken)
        {
            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[8192];
            int total = 0;
            string request = string.Empty;

            while (!request.Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                int read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);

                if (read == 0)
                {
                    throw new IOException("The client closed the connection during the handshake.");
                }

                total += read;
                request = Encoding.ASCII.GetString(buffer, 0, total);
            }

            string key = request
                .Split("\r\n")
                .First(line => line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                .Split(':')[1]
                .Trim();

            string accept = Convert.ToBase64String(
                System.Security.Cryptography.SHA1.HashData(
                    Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

            byte[] response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Accept: " + accept + "\r\n\r\n");

            await stream.WriteAsync(response, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _listener.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }

    [Fact]
    public async Task AHalfClosedSocketIsReplacedSoTheJetstreamCanStillReconnect()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer { DrainSockets = false };

        await server.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        using var jetstream = new AtProtoJetstream(uri: server.Uri);

        using var httpClient = new HttpClient();

        // A socket whose close the server never answers is left in CloseSent. It is neither Open, so a reconnection
        // cannot skip it, nor Closed or Aborted, which used to be the only states a reconnection replaced a socket for,
        // and a ClientWebSocket can only be connected once.
        ClientWebSocket halfClosed = new();

        await halfClosed.ConnectAsync(server.Uri, httpClient, cancellationToken);
        await halfClosed.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);

        Assert.Equal(WebSocketState.CloseSent, halfClosed.State);

        GetClientField(jetstream).SetValue(jetstream, halfClosed);

        await jetstream.ConnectAsync(
            uri: server.Uri,
            cursor: null,
            httpClient: httpClient,
            cancellationToken: cancellationToken);

        Assert.True(jetstream.IsConnected);
        Assert.NotSame(halfClosed, GetClient(jetstream));
    }

    [Fact]
    public async Task AReconnectionDoesNotInheritThePreviousConnectionsGracefulDisconnection()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        using var jetstream = new AtProtoJetstream(uri: server.Uri);

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(
            uri: server.Uri,
            cursor: null,
            httpClient: httpClient,
            cancellationToken: cancellationToken);

        await jetstream.CloseAsync(cancellationToken: cancellationToken);

        Assert.True(jetstream.DisconnectedGracefully);

        await jetstream.ConnectAsync(
            uri: server.Uri,
            cursor: null,
            httpClient: httpClient,
            cancellationToken: cancellationToken);

        Assert.True(jetstream.IsConnected);

        // Read from the field rather than the property, as the property is gated on the socket being closed and so
        // answers false for any open connection, whatever the flag beneath it says.
        Assert.False(GetDisconnectedGracefully(jetstream));
    }

    [Fact]
    public void AJetstreamAppliesAKeepAliveTimeoutSoAnUnresponsivePeerIsNoticed()
    {
        using var jetstream = new AtProtoJetstream();

        ClientWebSocket client = GetClient(jetstream);

        // A keep-alive interval on its own only sends pings. Without a timeout nothing acts on a peer which never
        // answers one, so a connection lost to a network failure is read from forever.
#if NET9_0_OR_GREATER
        Assert.Equal(TimeSpan.FromSeconds(30), client.Options.KeepAliveTimeout);
#endif
        Assert.Equal(TimeSpan.FromSeconds(30), client.Options.KeepAliveInterval);
    }

    [Fact]
    public void ConfiguredKeepAliveOptionsAreAppliedToTheWebSocket()
    {
        using var jetstream = new AtProtoJetstream(
            webSocketOptions: new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(5),
                KeepAliveTimeout = TimeSpan.FromSeconds(7)
            });

        ClientWebSocket client = GetClient(jetstream);

        Assert.Equal(TimeSpan.FromSeconds(5), client.Options.KeepAliveInterval);
#if NET9_0_OR_GREATER
        Assert.Equal(TimeSpan.FromSeconds(7), client.Options.KeepAliveTimeout);
#endif
    }

    private static FieldInfo GetClientField(AtProtoJetstream jetstream) =>
        jetstream.GetType().GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static ClientWebSocket GetClient(AtProtoJetstream jetstream) =>
        (ClientWebSocket)GetClientField(jetstream).GetValue(jetstream)!;

    private static bool GetDisconnectedGracefully(AtProtoJetstream jetstream) =>
        (bool)jetstream
            .GetType()
            .GetField("_disconnectedGracefully", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(jetstream)!;

    private sealed class TestJetstreamServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private int _connectionCount;

        public Uri Uri { get; private set; } = new("ws://localhost");

        /// <summary>
        /// How long to wait before accepting a connection, to keep a connection attempt in flight.
        /// </summary>
        public TimeSpan AcceptDelay { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Whether to read from accepted sockets. A server which does not read never answers a close handshake.
        /// </summary>
        public bool DrainSockets { get; set; } = true;

        public Task Start(Func<WebSocket, int, CancellationToken, Task> onConnected)
        {
            StartListener();

            _ = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    HttpListenerContext context = await _listener.GetContextAsync();

                    if (AcceptDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(AcceptDelay, _cancellationTokenSource.Token);
                    }

                    HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);

                    int connectionNumber = Interlocked.Increment(ref _connectionCount);

                    _ = Task.Run(async () =>
                    {
                        using (WebSocket webSocket = webSocketContext.WebSocket)
                        {
                            await onConnected(webSocket, connectionNumber, _cancellationTokenSource.Token);

                            try
                            {
                                if (DrainSockets)
                                {
                                    byte[] buffer = new byte[16 * 1024];

                                    while (webSocket.State == WebSocketState.Open && !_cancellationTokenSource.IsCancellationRequested)
                                    {
                                        WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, _cancellationTokenSource.Token);

                                        if (result.MessageType == WebSocketMessageType.Close)
                                        {
                                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, _cancellationTokenSource.Token);
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    // Held open, and unread, until the test disposes the server.
                                    await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                            }
                            catch (WebSocketException)
                            {
                            }
                        }
                    }, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Binds the listener to a free port, retrying when another listener takes the port between it being found
        /// and the listener binding it. <see cref="HttpListener"/> has no equivalent of binding port zero, so the
        /// window cannot be closed, only retried out of.
        /// </summary>
        private void StartListener()
        {
            const int maximumAttempts = 20;

            for (int attempt = 1; ; attempt++)
            {
                int port = FreePort();

                _listener.Prefixes.Clear();
                _listener.Prefixes.Add(string.Create(CultureInfo.InvariantCulture, $"http://localhost:{port}/"));

                try
                {
                    _listener.Start();
                }
                catch (HttpListenerException) when (attempt < maximumAttempts)
                {
                    continue;
                }

                Uri = new Uri(string.Create(CultureInfo.InvariantCulture, $"ws://localhost:{port}"));

                return;
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                // Closing the listener flushes whatever response is still open. A test which abandons a connection
                // rather than completing the close handshake leaves a socket behind which the managed HttpListener
                // used on platforms without http.sys cannot write to, and it says so by throwing out of Close().
                // The server is being torn down either way, so there is nothing to report.
                _listener.Close();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (HttpListenerException)
            {
            }
            catch (SocketException)
            {
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }
        }
    }
}
