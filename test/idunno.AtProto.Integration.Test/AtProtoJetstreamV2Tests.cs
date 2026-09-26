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

using ZstdSharp;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class AtProtoJetstreamV2Tests
{
    private const string TestDid = "did:plc:g6ylltenitt4tp27bpwalh7b";
    private const string SubProtocol = "xrpc.v1.json";

    private static string IdentityEvent(long sequence) =>
        $$$"""
        {"$type":"message","payload":{"$type":"network.bsky.jetstream.subscribeEvents#identity","did":"{{{TestDid}}}","seq":{{{sequence}}},"time":"2026-09-26T00:19:35Z","identity":{"did":"{{{TestDid}}}","seq":1,"time":"2026-09-26T00:19:35Z"} } }
        """;

    [Fact]
    public async Task AnUncompressedV2EventIsDelivered()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, connectionNumber, serverCancellationToken) =>
        {
            await SendText(webSocket, IdentityEvent(12), serverCancellationToken);
        });

        TaskCompletionSource<AtJetstreamEvent> recordReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false });

        jetstream.KindFilter = [JetStreamEventKind.Identity];
        jetstream.RecordReceived += (sender, e) => recordReceived.TrySetResult(e.ParsedEvent);

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: 3, httpClient: httpClient, cancellationToken: cancellationToken);

        AtJetstreamEvent received = await recordReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        AtJetstreamIdentityEvent identityEvent = Assert.IsType<AtJetstreamIdentityEvent>(received);
        Assert.Equal(12, identityEvent.Sequence);
        Assert.Equal(12, jetstream.LastSequence);

        TestJetstreamServer.Connection connection = Assert.Single(server.Connections);
        Assert.Equal("/xrpc/network.bsky.jetstream.subscribeEvents", connection.Path);
        Assert.Equal(SubProtocol, connection.SubProtocol);
        Assert.Contains("kinds=identity", connection.Query, StringComparison.Ordinal);
        Assert.Contains("cursor=3", connection.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("compress", connection.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("zstdDictionary", connection.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACompressedV2EventIsDecompressedWithTheDictionaryTheServerServes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        byte[] dictionary = new JetstreamOptions().Dictionary!;
        uint dictionaryId = BitConverter.ToUInt32(dictionary, 4);

        using var server = new TestJetstreamServer { Dictionary = dictionary };

        await server.Start(async (webSocket, connectionNumber, serverCancellationToken) =>
        {
            using var compressor = new Compressor();
            compressor.LoadDictionary(dictionary);

            await webSocket.SendAsync(
                compressor.Wrap(Encoding.UTF8.GetBytes(IdentityEvent(7))).ToArray(),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                serverCancellationToken);
        });

        TaskCompletionSource<AtJetstreamEvent> recordReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var jetstream = new AtProtoJetstream(uri: server.Uri);

        jetstream.RecordReceived += (sender, e) => recordReceived.TrySetResult(e.ParsedEvent);

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        AtJetstreamEvent received = await recordReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Equal(7, received.Sequence);

        TestJetstreamServer.Connection connection = Assert.Single(server.Connections);
        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"zstdDictionary={dictionaryId}"), connection.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusedConnectionThrowsWithTheServersError()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer
        {
            RefuseWith = (_, _) => (HttpStatusCode.BadRequest, """{"error":"CursorTooOld","message":"cursor 1 below lookback floor"}""")
        };

        await server.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        using var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false });

        using var httpClient = new HttpClient();

        JetstreamConnectionException exception = await Assert.ThrowsAsync<JetstreamConnectionException>(
            () => jetstream.ConnectAsync(uri: server.Uri, cursor: 1, httpClient: httpClient, cancellationToken: cancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.NotNull(exception.ErrorDetail);
        Assert.Equal("CursorTooOld", exception.ErrorDetail.Error);
        Assert.Equal("cursor 1 below lookback floor", exception.ErrorDetail.Message);
        Assert.False(jetstream.IsConnected);
    }

    [Fact]
    public async Task AnOutdatedDictionaryIsDownloadedAgainAndTheConnectionRetried()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        byte[] dictionary = new JetstreamOptions().Dictionary!;

        using var server = new TestJetstreamServer
        {
            Dictionary = dictionary,
            RefuseWith = (_, upgradeAttempt) => upgradeAttempt == 1
                ? (HttpStatusCode.BadRequest, """{"error":"UnknownZstdDictionary","message":"current dictionary id is 1"}""")
                : null
        };

        await server.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        using var jetstream = new AtProtoJetstream(uri: server.Uri);

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        Assert.True(jetstream.IsConnected);
        Assert.Equal(2, server.DictionaryRequests);
    }

    [Fact]
    public async Task ChangingAFilterReconnectsFromTheLastSequenceWithoutRedeliveringIt()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, connectionNumber, serverCancellationToken) =>
        {
            if (connectionNumber == 1)
            {
                await SendText(webSocket, IdentityEvent(100), serverCancellationToken);
            }
            else
            {
                // The cursor is inclusive, so the server sends the event the cursor names again.
                await SendText(webSocket, IdentityEvent(100), serverCancellationToken);
                await SendText(webSocket, IdentityEvent(101), serverCancellationToken);
            }
        });

        ConcurrentQueue<long?> sequences = new();
        ConcurrentQueue<WebSocketState> stateChanges = new();
        TaskCompletionSource firstReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false, MaximumConcurrentMessageParsers = 1 });

        jetstream.RecordReceived += (sender, e) =>
        {
            sequences.Enqueue(e.ParsedEvent.Sequence);

            if (e.ParsedEvent.Sequence == 100)
            {
                firstReceived.TrySetResult();
            }
            else if (e.ParsedEvent.Sequence == 101)
            {
                secondReceived.TrySetResult();
            }
        };

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        jetstream.ConnectionStateChanged += (sender, e) => stateChanges.Enqueue(e.State);

        jetstream.DidFilter = [new Did(TestDid)];

        await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Equal([100, 101], sequences);
        Assert.True(jetstream.IsConnected);
        Assert.Equal(101, jetstream.LastSequence);

        Assert.Equal(2, server.Connections.Count);
        TestJetstreamServer.Connection reconnection = server.Connections.Last();
        Assert.Contains("cursor=100", reconnection.Query, StringComparison.Ordinal);
        Assert.Contains("dids=did%3aplc%3ag6ylltenitt4tp27bpwalh7b", reconnection.Query, StringComparison.OrdinalIgnoreCase);

        // The replaced connection is not reported as a disconnection, only the connection which replaced it.
        Assert.DoesNotContain(WebSocketState.Aborted, stateChanges);
        Assert.Contains(WebSocketState.Open, stateChanges);
    }

    [Fact]
    public async Task AnErrorFrameIsRaisedAsAFault()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, connectionNumber, serverCancellationToken) =>
        {
            await SendText(webSocket, """{"$type":"error","error":"ConsumerTooSlow","message":"too slow"}""", serverCancellationToken);
        });

        TaskCompletionSource<FaultRaisedEventArgs> faultRaised = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false });

        jetstream.FaultRaised += (sender, e) => faultRaised.TrySetResult(e);

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        FaultRaisedEventArgs fault = await faultRaised.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Equal("ConsumerTooSlow", fault.Error);
    }

    private static async Task SendText(WebSocket webSocket, string message, CancellationToken cancellationToken)
    {
        await webSocket.SendAsync(
            Encoding.UTF8.GetBytes(message),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }

    /// <summary>
    /// A minimal version 2 jetstream server, which serves a dictionary, can refuse upgrades, and accepts the
    /// subprotocol a client asks for.
    /// </summary>
    private sealed class TestJetstreamServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly ConcurrentQueue<Connection> _connections = new();
        private int _connectionCount;
        private int _dictionaryRequests;
        private int _upgradeAttempts;

        public TestJetstreamServer()
        {
            int port = FreePort();

            _listener.Prefixes.Add(string.Create(CultureInfo.InvariantCulture, $"http://localhost:{port}/"));
            Uri = new Uri(string.Create(CultureInfo.InvariantCulture, $"ws://localhost:{port}"));
        }

        public sealed record Connection(string Path, string Query, string? SubProtocol);

        public Uri Uri { get; }

        public byte[]? Dictionary { get; init; }

        /// <summary>
        /// Decides, from the query and the number of upgrade attempts made so far, whether to refuse a subscription and
        /// with what status and body. Consulted for both the upgrade and the plain request made afterwards to read why
        /// it was refused.
        /// </summary>
        public Func<string, int, (HttpStatusCode StatusCode, string Body)?>? RefuseWith { get; init; }

        public IReadOnlyCollection<Connection> Connections => [.. _connections];

        public int DictionaryRequests => _dictionaryRequests;

        public Task Start(Func<WebSocket, int, CancellationToken, Task> onConnected)
        {
            _listener.Start();

            _ = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    HttpListenerContext context = await _listener.GetContextAsync();

                    string path = context.Request.Url?.AbsolutePath ?? string.Empty;
                    string query = context.Request.Url?.Query ?? string.Empty;

                    if (string.Equals(path, "/xrpc/network.bsky.jetstream.getZstdDictionary", StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref _dictionaryRequests);
                        await Respond(context, HttpStatusCode.OK, "application/octet-stream", Dictionary ?? []);
                        continue;
                    }

                    if (!context.Request.IsWebSocketRequest)
                    {
                        (HttpStatusCode StatusCode, string Body)? refusal = RefuseWith?.Invoke(query, Volatile.Read(ref _upgradeAttempts));

                        if (refusal is not null)
                        {
                            await Respond(context, refusal.Value.StatusCode, "application/json", Encoding.UTF8.GetBytes(refusal.Value.Body));
                        }
                        else
                        {
                            await Respond(context, HttpStatusCode.UpgradeRequired, "text/plain", []);
                        }

                        continue;
                    }

                    (HttpStatusCode StatusCode, string Body)? upgradeRefusal = RefuseWith?.Invoke(query, Interlocked.Increment(ref _upgradeAttempts));

                    if (upgradeRefusal is not null)
                    {
                        await Respond(context, upgradeRefusal.Value.StatusCode, "application/json", Encoding.UTF8.GetBytes(upgradeRefusal.Value.Body));
                        continue;
                    }

                    string? subProtocol = context.Request.Headers["Sec-WebSocket-Protocol"];

                    HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(subProtocol);

                    _connections.Enqueue(new Connection(path, query, webSocketContext.WebSocket.SubProtocol));

                    int connectionNumber = Interlocked.Increment(ref _connectionCount);

                    _ = Task.Run(async () =>
                    {
                        using (WebSocket webSocket = webSocketContext.WebSocket)
                        {
                            try
                            {
                                await onConnected(webSocket, connectionNumber, _cancellationTokenSource.Token);

                                // Read until the client closes and the close answered, as a real server does.
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

        private static async Task Respond(HttpListenerContext context, HttpStatusCode statusCode, string contentType, byte[] body)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = body.Length;

            await context.Response.OutputStream.WriteAsync(body);

            context.Response.Close();
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            try
            {
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
