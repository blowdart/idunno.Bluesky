// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;

using ZstdSharp;

using idunno.AtProto.Jetstream;
using idunno.Bluesky;
using idunno.Bluesky.Feed;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Captures the corpora the benchmarks replay from the live, public, unauthenticated firehose, jetstream and AppView, and
/// the authenticated home timeline of the account in the <c>_BlueskyHandle</c> and <c>_BlueskyPassword</c> environment variables.
/// </summary>
/// <remarks>
/// <para>Only the payload of each web socket message, or the body of each HTTP response, is recorded. No request or
/// response headers are recorded, and any message which <see cref="SensitiveContent"/> flags is discarded rather than written.</para>
/// <para>The timeline has every entry which mentions the capturing account's handle or password removed, and the account's
/// DID replaced by <see cref="CaptureScrubber.PlaceholderDid"/>. It is not written if anything identifying remains.</para>
/// </remarks>
internal static class Capture
{
    private static readonly Uri s_firehose = new("wss://bsky.network/xrpc/com.atproto.sync.subscribeRepos");
    private static readonly Uri s_jetstreamV1 = new("wss://jetstream1.us-west.bsky.network/subscribe");
    private static readonly Uri s_jetstreamV1Zstd = new("wss://jetstream1.us-west.bsky.network/subscribe?compress=true");
    private static readonly Uri s_jetstreamV2 = new("wss://jetstream.us-west.bsky.network/xrpc/network.bsky.jetstream.subscribeEvents");
    private static readonly Uri s_getAuthorFeed = new("https://public.api.bsky.app/xrpc/app.bsky.feed.getAuthorFeed?actor=bsky.app&limit=50");
    private static readonly Uri s_getProfiles = new("https://public.api.bsky.app/xrpc/app.bsky.actor.getProfiles?actors=bsky.app&actors=atproto.com&actors=safety.bsky.app&actors=jay.bsky.team&actors=pfrazee.com");

    public static string DefaultOutputDirectory()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "benchmarks", "idunno.AtProto.Benchmarks", "Corpus");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(Environment.CurrentDirectory, "Corpus");
    }

    public static async Task RunAsync(string outputDirectory, bool xrpcOnly, string handle, string password)
    {
        Directory.CreateDirectory(outputDirectory);

        await CaptureXrpcAsync(outputDirectory, CorpusFile.XrpcGetAuthorFeed, s_getAuthorFeed).ConfigureAwait(false);
        await CaptureXrpcAsync(outputDirectory, CorpusFile.XrpcGetProfiles, s_getProfiles).ConfigureAwait(false);
        await CaptureTimelineAsync(outputDirectory, handle, password).ConfigureAwait(false);

        if (xrpcOnly)
        {
            return;
        }

        using Decompressor decompressor = new();
        JetstreamOptions options = new();
        decompressor.LoadDictionary(options.Dictionary);

        await CaptureAsync(outputDirectory, CorpusFile.Firehose, s_firehose, null, static m => m).ConfigureAwait(false);
        await CaptureAsync(outputDirectory, CorpusFile.JetstreamV1, s_jetstreamV1, null, static m => m).ConfigureAwait(false);
        await CaptureAsync(outputDirectory, CorpusFile.JetstreamV1Zstd, s_jetstreamV1Zstd, null, m => decompressor.Unwrap(m, options.MaxMessageSize).ToArray()).ConfigureAwait(false);
        await CaptureAsync(outputDirectory, CorpusFile.JetstreamV2, s_jetstreamV2, "xrpc.v1.json", static m => m).ConfigureAwait(false);
    }

    private static async Task CaptureTimelineAsync(string outputDirectory, string handle, string password)
    {
        using BlueskyAgent agent = new();
        AtProtoHttpResult<bool> login = await agent.Login(handle, password).ConfigureAwait(false);
        if (!login.Succeeded || agent.Credentials is null || agent.Did is null)
        {
            throw new InvalidOperationException($"Could not log in to capture the timeline, the server returned {login.StatusCode}.");
        }

        try
        {
            // The timeline is fetched through the library, so the request is the one the benchmark replays, but only the
            // response body is recorded. Request and response headers, including authorization, are never kept.
            using BodyRecordingHandler recorder = new();
            using HttpClient httpClient = new(recorder);

            AtProtoHttpResult<Timeline> timeline =
                await BlueskyServer.GetTimeline(null, 50, null, agent.Service, agent.Credentials, httpClient).ConfigureAwait(false);
            if (!timeline.Succeeded || recorder.Body is null)
            {
                throw new InvalidOperationException($"Could not capture the timeline, the server returned {timeline.StatusCode}.");
            }

            string viewerDid = agent.Did.ToString();
            string[] secrets = [handle, password, viewerDid, agent.Credentials.AccessJwt, agent.Credentials.RefreshToken];

            byte[] body = CaptureScrubber.ScrubFeed(recorder.Body, viewerDid, [handle, password], out int removed);

            if (CaptureScrubber.ContainsAny(body, secrets) || SensitiveContent.IsSensitive(body))
            {
                throw new InvalidOperationException($"The {CorpusFile.XrpcGetTimeline} response still identifies the capturing account after scrubbing, so it was not written.");
            }

            string path = Path.Combine(outputDirectory, CorpusFile.FileName(CorpusFile.XrpcGetTimeline));
            CorpusFile.Write(path, [body]);

            Console.WriteLine($"{CorpusFile.XrpcGetTimeline}: {body.Length:N0} bytes, {removed} entries removed as identifying, {new FileInfo(path).Length:N0} bytes compressed.");
        }
        finally
        {
            await agent.Logout().ConfigureAwait(false);
        }
    }

    private static async Task CaptureXrpcAsync(string outputDirectory, string name, Uri uri)
    {
        // An anonymous request to the public AppView. Only the response body is kept.
        using HttpClient httpClient = new();
        using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        byte[] body = await CaptureContent.ReadAsync(response.Content, AtProtoHttpClient.DefaultMaximumResponseSize, CancellationToken.None).ConfigureAwait(false);

        if (SensitiveContent.IsSensitive(body))
        {
            throw new InvalidOperationException($"The {name} response looks like it contains a credential, so it was not written.");
        }

        string path = Path.Combine(outputDirectory, CorpusFile.FileName(name));
        CorpusFile.Write(path, [body]);

        Console.WriteLine($"{name}: {body.Length:N0} bytes, {new FileInfo(path).Length:N0} bytes compressed.");
    }

    private static async Task CaptureAsync(string outputDirectory, string name, Uri uri, string? subProtocol, Func<byte[], byte[]> plainText)
    {
        int count = CorpusFile.MessageCount(name);

        using ClientWebSocket socket = new();
        if (subProtocol is not null)
        {
            socket.Options.AddSubProtocol(subProtocol);
        }

        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(5));
        await socket.ConnectAsync(uri, timeout.Token).ConfigureAwait(false);

        List<byte[]> messages = new(count);
        int discarded = 0;
        int maximumMessageSize = new JetstreamOptions().MaxMessageSize;

        while (messages.Count < count)
        {
            (WebSocketReceiveResult result, byte[] message) = await socket.ReceiveNextMessageAsync(
                64 * 1024, maximumMessageSize, cancellationToken: timeout.Token).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidDataException("The capture stream closed before enough messages were received.");
            }

            if (SensitiveContent.IsSensitive(plainText(message)))
            {
                discarded++;
                continue;
            }

            messages.Add(message);
        }

        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);

        string path = Path.Combine(outputDirectory, CorpusFile.FileName(name));
        CorpusFile.Write(path, messages);

        Console.WriteLine($"{name}: {messages.Count} messages, {discarded} discarded as possibly sensitive, {new FileInfo(path).Length:N0} bytes compressed.");
    }

    private sealed class BodyRecordingHandler() : DelegatingHandler(new HttpClientHandler())
    {
        public byte[]? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            byte[] body;
            try
            {
                body = await CaptureContent.ReadAsync(response.Content, AtProtoHttpClient.DefaultMaximumResponseSize, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                response.Dispose();
                throw;
            }
            if (response.IsSuccessStatusCode)
            {
                Body = body;
            }

            ByteArrayContent replacement = new(body);
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
            {
                replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content.Dispose();
            response.Content = replacement;
            return response;
        }
    }
}