// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Events;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;

using ZstdSharp;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class AtProtoJetstreamReceiveTests
{
    private const string Did = "did:plc:g6ylltenitt4tp27bpwalh7b";

    private static string IdentityEvent(long sequence = 1, string padding = "") => $$"""
        {
          "did":"{{Did}}",
          "time_us":1746663645473657,
          "kind":"identity",
          "identity": {
            "did":"{{Did}}",
            "handle":"miyakotubaki.bsky.social",
            "seq":{{sequence}},
            "time":"2025-05-08T00:20:44.859Z"},
          "padding":"{{padding}}"
        }
        """;

    [Fact]
    public async Task AnUncompressedMessageIsDeliveredToRecordReceived()
    {
        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, cancellationToken) =>
        {
            await SendText(webSocket, IdentityEvent(), cancellationToken);
        });

        AtJetstreamEvent received = await Receive(server, useCompression: false);

        Assert.Equal(Did, received.Did);
        Assert.Equal(JetStreamEventKind.Identity, received.Kind);
    }

    [Fact]
    public async Task ACompressedMessageIsDeliveredToRecordReceived()
    {
        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, cancellationToken) =>
        {
            await SendCompressed(webSocket, IdentityEvent(), cancellationToken);
        });

        AtJetstreamEvent received = await Receive(server, useCompression: true);

        Assert.Equal(Did, received.Did);
        Assert.Equal(JetStreamEventKind.Identity, received.Kind);
    }

    [Fact]
    public async Task ACompressedMessageWhichExpandsBeyondTheMaximumMessageSizeIsDropped()
    {
        const int maximumMessageSize = 64 * 1024;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, cancellationToken) =>
        {
            // A small frame which expands to far more than the maximum message size. Without a limit on the
            // decompressed output this is allocated in full, however large the sender declares it to be.
            await SendCompressed(webSocket, IdentityEvent(sequence: 1, padding: new string('a', maximumMessageSize * 8)), cancellationToken);

            // Sent afterwards so the assertion does not depend on waiting out a timeout. Messages are read from the
            // socket in order, so by the time this one is delivered the first has already been handled or dropped.
            await SendCompressed(webSocket, IdentityEvent(sequence: 2), cancellationToken);
        });

        ConcurrentQueue<string> messages = [];

        AtJetstreamEvent received = await Receive(
            server,
            useCompression: true,
            maximumMessageSize: maximumMessageSize,
            onMessageReceived: messages.Enqueue);

        AtJetstreamIdentityEvent identityEvent = Assert.IsType<AtJetstreamIdentityEvent>(received);

        Assert.Equal(2U, identityEvent.Identity.Sequence);

        // Records are parsed on the task factory, so which one is parsed first is a race. What is not a race is
        // whether the over-large message was decompressed at all, which is what MessageReceived reports.
        Assert.Single(messages);
        Assert.True(
            messages.Single().Length <= maximumMessageSize,
            $"A message of {messages.Single().Length} bytes was decompressed, over the {maximumMessageSize} byte limit.");
    }

    [Fact]
    public async Task TheMaximumMessageSizeIsSentToTheServer()
    {
        const int maximumMessageSize = 64 * 1024;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, cancellationToken) =>
        {
            await SendCompressed(webSocket, IdentityEvent(), cancellationToken);
        });

        _ = await Receive(server, useCompression: true, maximumMessageSize: maximumMessageSize);

        Assert.NotNull(server.RequestQuery);

        Assert.Contains(
            string.Create(CultureInfo.InvariantCulture, $"maxMessageSizeBytes={maximumMessageSize}"),
            server.RequestQuery,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AServerInitiatedCloseIsRecordedAsAGracefulDisconnection()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, serverCancellationToken) =>
        {
            await SendText(webSocket, IdentityEvent(), serverCancellationToken);
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server disconnect", serverCancellationToken);
        });

        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { ProtocolVersion = JetstreamProtocolVersion.V1, UseCompression = false }))
        {
            jetstream.ConnectionStateChanged += (sender, e) =>
            {
                if (e.State == WebSocketState.Closed)
                {
                    closed.TrySetResult();
                }
            };

            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                await closed.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

                Assert.Equal(WebSocketState.Closed, jetstream.State);
                Assert.True(jetstream.DisconnectedGracefully);
            }
        }
    }

    [Fact]
    public async Task ConcurrentConnectionAttemptsProduceASingleConnection()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer
        {
            // Holds the first connection attempt open long enough for the others to be made whilst it is in flight.
            AcceptDelay = TimeSpan.FromSeconds(1)
        };

        await server.Start(async (webSocket, serverCancellationToken) =>
        {
            await SendText(webSocket, IdentityEvent(), serverCancellationToken);
        });

        ConcurrentQueue<string> messages = [];

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { ProtocolVersion = JetstreamProtocolVersion.V1, UseCompression = false }))
        {
            jetstream.MessageReceived += (sender, e) => messages.Enqueue(e.Message);

            TaskCompletionSource recordReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
            jetstream.RecordReceived += (sender, e) => recordReceived.TrySetResult();

            using (var httpClient = new HttpClient())
            {
                Task[] connections =
                [
                    .. Enumerable.Range(0, 4).Select(_ => Task.Run(
                        async () => await jetstream.ConnectAsync(
                            uri: server.Uri,
                            cursor: null,
                            httpClient: httpClient,
                            cancellationToken: cancellationToken),
                        cancellationToken))
                ];

                await Task.WhenAll(connections).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

                Assert.True(jetstream.IsConnected);

                await recordReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

                Assert.Equal(1, server.ConnectionCount);

                // One connection carrying one message, read by one receive loop, delivers the message once.
                Assert.Single(messages);
            }
        }
    }

    [Fact]
    public async Task AMessageMadeUpOfEmptyFragmentsIsAbandoned()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, serverCancellationToken) =>
        {
            // An empty fragment adds nothing to the message being assembled, so the maximum message size can never
            // bring this to an end. Only a limit on the fragments themselves can.
            for (int i = 0; i < 100; i++)
            {
                await webSocket.SendAsync(
                    Array.Empty<byte>(),
                    WebSocketMessageType.Text,
                    endOfMessage: false,
                    serverCancellationToken);
            }

            await webSocket.SendAsync(
                Array.Empty<byte>(),
                WebSocketMessageType.Text,
                endOfMessage: true,
                serverCancellationToken);

            await SendText(webSocket, IdentityEvent(sequence: 2), serverCancellationToken);
        });

        ConcurrentQueue<string> faults = [];

        TaskCompletionSource<AtJetstreamEvent> recordReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<WebSocketState> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions
            {
                ProtocolVersion = JetstreamProtocolVersion.V1,
                UseCompression = false,

                // This server never reads, so it never answers the close the jetstream sends. Without a shorter deadline
                // the connection is only dropped once the default close timeout expires.
                CloseTimeout = TimeSpan.FromSeconds(2)
            }))
        {
            jetstream.FaultRaised += (sender, e) => faults.Enqueue(e.Fault);
            jetstream.RecordReceived += (sender, e) => recordReceived.TrySetResult(e.ParsedEvent);
            jetstream.ConnectionStateChanged += (sender, e) =>
            {
                if (e.State != WebSocketState.Open)
                {
                    disconnected.TrySetResult(e.State);
                }
            };

            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

                Assert.Contains(faults, fault => fault.Contains("empty fragments", StringComparison.Ordinal));

                // The fragments making up the rest of the abandoned message are still queued on the socket, and there is
                // no way to skip them, so the connection is dropped rather than read on into the middle of a message.
                Assert.False(jetstream.IsConnected);

                // Which also means the message the server sent after the abandoned one is never delivered.
                Assert.False(recordReceived.Task.IsCompleted);

                // The disconnection was forced by the message, not performed by either end deciding to close.
                Assert.False(jetstream.DisconnectedGracefully);
            }
        }
    }

    [Fact]
    public void SettingAFilterToNullThrows()
    {
        using var jetstream = new AtProtoJetstream();

        ArgumentNullException didFilterException = Assert.Throws<ArgumentNullException>(() => jetstream.DidFilter = null!);
        ArgumentNullException collectionFilterException = Assert.Throws<ArgumentNullException>(() => jetstream.CollectionFilter = null!);

        // The parameter name is checked because a null slips through to the copy of the value and throws from there
        // too, naming whichever parameter the copy happened to use rather than the value which was set.
        Assert.Equal("value", didFilterException.ParamName);
        Assert.Equal("value", collectionFilterException.ParamName);
    }

    [Fact]
    public async Task AReconnectionDoesNotInheritThePreviousConnectionsLastMessageTimestamp()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, serverCancellationToken) =>
        {
            await SendText(webSocket, IdentityEvent(), serverCancellationToken);
        });

        TaskCompletionSource recordReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { ProtocolVersion = JetstreamProtocolVersion.V1, UseCompression = false, CloseTimeout = TimeSpan.FromSeconds(1) });

        jetstream.RecordReceived += (sender, e) => recordReceived.TrySetResult();

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        await recordReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.NotNull(jetstream.MessageLastReceived);

        await jetstream.CloseAsync(cancellationToken: cancellationToken);

        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        // A stale timestamp from the previous connection would make a watchdog think the new connection had already
        // delivered something.
        Assert.Null(jetstream.MessageLastReceived);
    }

    private static async Task<AtJetstreamEvent> Receive(
        TestJetstreamServer server,
        bool useCompression,
        int maximumMessageSize = 1024 * 1024,
        Action<string>? onMessageReceived = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TaskCompletionSource<AtJetstreamEvent> recordReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions
            {
                ProtocolVersion = JetstreamProtocolVersion.V1,
                UseCompression = useCompression,
                MaxMessageSize = maximumMessageSize
            }))
        {
            jetstream.RecordReceived += (sender, e) => recordReceived.TrySetResult(e.ParsedEvent);

            if (onMessageReceived is not null)
            {
                jetstream.MessageReceived += (sender, e) => onMessageReceived(e.Message);
            }

            // The jetstream's own HttpClient applies SSRF protection, which a loopback test server cannot pass.
            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                Assert.True(jetstream.IsConnected);

                return await recordReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
    }

    [Fact]
    public async Task AMessageWhichExceedsTheMaximumMessageSizeDropsTheConnection()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, serverCancellationToken) =>
        {
            await SendText(webSocket, IdentityEvent(sequence: 1, padding: new string('a', 8192)), serverCancellationToken);

            await SendText(webSocket, IdentityEvent(sequence: 2), serverCancellationToken);
        });

        ConcurrentQueue<string> faults = [];

        TaskCompletionSource<AtJetstreamEvent> recordReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<WebSocketState> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions
            {
                ProtocolVersion = JetstreamProtocolVersion.V1,
                UseCompression = false,
                MaxMessageSize = 2048,

                // This server never reads, so it never answers the close the jetstream sends. Without a shorter deadline
                // the connection is only dropped once the default close timeout expires.
                CloseTimeout = TimeSpan.FromSeconds(2)
            }))
        {
            jetstream.FaultRaised += (sender, e) => faults.Enqueue(e.Fault);
            jetstream.RecordReceived += (sender, e) => recordReceived.TrySetResult(e.ParsedEvent);
            jetstream.ConnectionStateChanged += (sender, e) =>
            {
                if (e.State != WebSocketState.Open)
                {
                    disconnected.TrySetResult(e.State);
                }
            };

            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

                Assert.Contains(faults, fault => fault.Contains("maximum allowed size", StringComparison.Ordinal));

                // The rest of the oversized message is still queued on the socket, so reading on would hand its tail to
                // the parser as though it were a message of its own.
                Assert.False(jetstream.IsConnected);
                Assert.False(recordReceived.Task.IsCompleted);
                Assert.False(jetstream.DisconnectedGracefully);
            }
        }
    }

    [Fact]
    public async Task NoMoreMessagesAreParsedAtOnceThanTheConfiguredLimit()
    {
        const int messageCount = 8;

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, serverCancellationToken) =>
        {
            for (int sequence = 1; sequence <= messageCount; sequence++)
            {
                await SendText(webSocket, IdentityEvent(sequence), serverCancellationToken);
            }
        });

        int inFlight = 0;
        int maximumInFlight = 0;
        object maximumLock = new();

        using var allParsed = new CountdownEvent(messageCount);

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { ProtocolVersion = JetstreamProtocolVersion.V1, UseCompression = false, MaximumConcurrentMessageParsers = 1 }))
        {
            jetstream.RecordReceived += (sender, e) =>
            {
                int current = Interlocked.Increment(ref inFlight);

                lock (maximumLock)
                {
                    maximumInFlight = Math.Max(maximumInFlight, current);
                }

                // Held long enough that anything dispatched alongside this one overlaps it.
                Thread.Sleep(50);

                Interlocked.Decrement(ref inFlight);
                allParsed.Signal();
            };

            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                Assert.True(allParsed.Wait(TimeSpan.FromSeconds(30), cancellationToken));

                // Without a limit every message read is handed straight to the task factory, so a server which sends
                // faster than the parsing keeps up with has all of them in flight at once and nothing bounds the queue.
                lock (maximumLock)
                {
                    Assert.Equal(1, maximumInFlight);
                }
            }
        }
    }

    [Fact]
    public async Task AThrowingRecordReceivedHandlerDoesNotDropTheConnectionOrCountAsAParsingFailure()
    {
        const int messageCount = 4;

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, serverCancellationToken) =>
        {
            for (int sequence = 1; sequence <= messageCount; sequence++)
            {
                await SendText(webSocket, IdentityEvent(sequence), serverCancellationToken);
            }
        });

        using var meterFactory = new TestMeterFactory();
        using var allHandled = new CountdownEvent(messageCount);

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { ProtocolVersion = JetstreamProtocolVersion.V1, UseCompression = false, MeterFactory = meterFactory }))
        {
            using var collector = new MetricCollector<long>(meterFactory, "idunno.AtProto.Jetstream", "idunno.atproto.jetstream.total.message_parsing_failures");

            jetstream.RecordReceived += (sender, e) =>
            {
                allHandled.Signal();

                throw new InvalidOperationException("Handler failure.");
            };

            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                Assert.True(allHandled.Wait(TimeSpan.FromSeconds(30), cancellationToken));

                // The handler signals before it throws, so the catch which decides whether to count the failure has
                // not necessarily run yet. Give it time to land rather than reading the counter out from under it.
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                // A handler is application code. An exception out of one says nothing about the message, so counting
                // it as a parsing failure makes an application bug indistinguishable from a malformed server message.
                Assert.Equal(0, collector.GetMeasurementSnapshot().Sum(measurement => measurement.Value));

                // The slot taken for each message is still given back, so reading carries on.
                Assert.True(jetstream.IsConnected);
            }
        }
    }

    [Fact]
    public async Task AMessageParserWhichCannotBeStartedDoesNotStallTheReceiveLoop()
    {
        // More messages than there are parser slots. Each failure used to keep the slot it took, so once this many
        // had failed the receive loop waited for a slot which nothing was ever going to give back.
        const int messageCount = 5;

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, serverCancellationToken) =>
        {
            for (int sequence = 1; sequence <= messageCount; sequence++)
            {
                await SendText(webSocket, IdentityEvent(sequence), serverCancellationToken);
            }
        });

        using var allRead = new CountdownEvent(messageCount);

        using (var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions
            {
                ProtocolVersion = JetstreamProtocolVersion.V1,
                UseCompression = false,
                MaximumConcurrentMessageParsers = 2,
                TaskFactory = new TaskFactory(new RefusingTaskScheduler())
            }))
        {
            jetstream.MessageReceived += (sender, e) =>
            {
                if (!allRead.IsSet)
                {
                    allRead.Signal();
                }
            };

            using (var httpClient = new HttpClient())
            {
                await jetstream.ConnectAsync(
                    uri: server.Uri,
                    cursor: null,
                    httpClient: httpClient,
                    cancellationToken: cancellationToken);

                Assert.True(
                    allRead.Wait(TimeSpan.FromSeconds(30), cancellationToken),
                    $"The receive loop stopped reading after {messageCount - allRead.CurrentCount} of {messageCount} messages.");
            }
        }
    }

    /// <summary>
    /// A <see cref="TaskScheduler"/> which refuses everything queued to it.
    /// </summary>
    private sealed class RefusingTaskScheduler : TaskScheduler
    {
        protected override void QueueTask(Task task) => throw new InvalidOperationException("This scheduler accepts nothing.");

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        protected override IEnumerable<Task> GetScheduledTasks() => [];
    }

    private static async Task SendText(WebSocket webSocket, string message, CancellationToken cancellationToken)
    {
        await webSocket.SendAsync(
            Encoding.UTF8.GetBytes(message),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }

    private static async Task SendCompressed(WebSocket webSocket, string message, CancellationToken cancellationToken)
    {
        using var compressor = new Compressor();

        byte[] compressed = compressor.Wrap(Encoding.UTF8.GetBytes(message)).ToArray();

        await webSocket.SendAsync(
            compressed,
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cancellationToken);
    }

    private sealed class TestJetstreamServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private int _connectionCount;

        public TestJetstreamServer()
        {
            int port = FreePort();

            _listener.Prefixes.Add(string.Create(CultureInfo.InvariantCulture, $"http://localhost:{port}/"));
            Uri = new Uri(string.Create(CultureInfo.InvariantCulture, $"ws://localhost:{port}"));
        }

        public Uri Uri { get; }

        public string? RequestQuery { get; private set; }

        /// <summary>
        /// The number of web socket connections the server has accepted.
        /// </summary>
        public int ConnectionCount => _connectionCount;

        /// <summary>
        /// How long to wait before accepting a connection, to keep a connection attempt in flight whilst another is made.
        /// </summary>
        public TimeSpan AcceptDelay { get; set; } = TimeSpan.Zero;

        public Task Start(Func<WebSocket, CancellationToken, Task> onConnected)
        {
            _listener.Start();

            _ = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    HttpListenerContext context = await _listener.GetContextAsync();

                    RequestQuery = context.Request.Url?.Query;

                    if (AcceptDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(AcceptDelay, _cancellationTokenSource.Token);
                    }

                    HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);

                    // Every connection is counted, but only the first is sent anything, so a test can tell a second
                    // connection apart from the first by what arrives on it as well as by the count.
                    bool first = Interlocked.Increment(ref _connectionCount) == 1;

                    _ = Task.Run(async () =>
                    {
                        using (WebSocket webSocket = webSocketContext.WebSocket)
                        {
                            if (first)
                            {
                                await onConnected(webSocket, _cancellationTokenSource.Token);
                            }

                            // Held open until the test disposes the server, as closing drops the connection under the client.
                            try
                            {
                                await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
                            }
                            catch (OperationCanceledException)
                            {
                            }
                        }
                    }, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                // Abort() rather than Close(). Tests here deliberately abandon a connection instead of completing the
                // close handshake, and Close() is the graceful path, so it tries to write the end of the response to a
                // socket the client has already torn down. The managed HttpListener used on platforms without http.sys
                // throws out of that write, and whether it does is a race against the listener reaping the connection,
                // which made CI fail intermittently. Abort() discards the connections without flushing them.
                //
                // The catches mirror AtProtoJetstreamConnectionTests. The server is being torn down either way, so
                // there is nothing to report.
                _listener.Abort();
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
    }
}
