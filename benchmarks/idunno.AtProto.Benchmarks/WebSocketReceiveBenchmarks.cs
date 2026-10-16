// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Measures reassembling captured messages from web socket fragments, per message.
/// </summary>
[MemoryDiagnoser]
public class WebSocketReceiveBenchmarks
{
    private const int MaxMessageSize = 5 * 1024 * 1024;

    private readonly ReplayWebSocket _socket = new();
    private byte[][] _firehose = null!;
    private byte[][] _jetstream = null!;

    [GlobalSetup]
    public void Setup()
    {
        _firehose = CorpusFile.Read(CorpusFile.Firehose);
        _jetstream = CorpusFile.Read(CorpusFile.JetstreamV1);
    }

    [Benchmark(OperationsPerInvoke = CorpusFile.FirehoseMessageCount)]
    public Task<long> Firehose() => ReceiveAll(_firehose);

    [Benchmark(OperationsPerInvoke = CorpusFile.JetstreamMessageCount)]
    public Task<long> Jetstream() => ReceiveAll(_jetstream);

    private async Task<long> ReceiveAll(byte[][] messages)
    {
        long total = 0;
        foreach (byte[] message in messages)
        {
            _socket.Load(message);
            (_, byte[] received) = await _socket.ReceiveNextMessageAsync(ReplayWebSocket.FragmentSize, MaxMessageSize).ConfigureAwait(false);
            total += received.Length;
        }

        return total;
    }
}