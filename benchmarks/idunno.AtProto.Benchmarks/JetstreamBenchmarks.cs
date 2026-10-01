// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

using BenchmarkDotNet.Attributes;
using ZstdSharp;

using idunno.AtProto.Jetstream;

namespace idunno.AtProto.Benchmarks;

#pragma warning disable CS0618 // AtJetstreamEvent is obsolete, but is still what the V1 receive path produces.

/// <summary>
/// Measures turning a received jetstream message into an event, per message, following the receive loop and parser.
/// </summary>
[MemoryDiagnoser]
public class JetstreamBenchmarks
{
    private const int MaxMessageSize = 5 * 1024 * 1024;

    private byte[][] _v1 = null!;
    private byte[][] _v1Zstd = null!;
    private byte[][] _v2 = null!;
    private Decompressor _decompressor = null!;
    private AtProtoJetstream _v1Jetstream = null!;
    private AtProtoJetstream _v2Jetstream = null!;

    [GlobalSetup]
    public void Setup()
    {
        _v1 = CorpusFile.Read(CorpusFile.JetstreamV1);
        _v1Zstd = CorpusFile.Read(CorpusFile.JetstreamV1Zstd);
        _v2 = CorpusFile.Read(CorpusFile.JetstreamV2);

        _decompressor = new Decompressor();
        _decompressor.LoadDictionary(new JetstreamOptions().Dictionary);

        _v1Jetstream = new AtProtoJetstream(options: new JetstreamOptions { ProtocolVersion = JetstreamProtocolVersion.V1, UseCompression = false });
        _v2Jetstream = new AtProtoJetstream(options: new JetstreamOptions { ProtocolVersion = JetstreamProtocolVersion.V2, UseCompression = false });
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _decompressor.Dispose();
        _v1Jetstream.Dispose();
        _v2Jetstream.Dispose();
    }

    [Benchmark(OperationsPerInvoke = CorpusFile.JetstreamMessageCount)]
    public int V1()
    {
        int parsed = 0;
        foreach (byte[] message in _v1)
        {
            parsed += ParseV1(message);
        }

        return parsed;
    }

    [Benchmark(OperationsPerInvoke = CorpusFile.JetstreamMessageCount)]
    public int V1Compressed()
    {
        int parsed = 0;
        foreach (byte[] message in _v1Zstd)
        {
            parsed += ParseV1(_decompressor.Unwrap(message, MaxMessageSize).ToArray());
        }

        return parsed;
    }

    [Benchmark(OperationsPerInvoke = CorpusFile.JetstreamMessageCount)]
    public int V2()
    {
        int parsed = 0;
        foreach (byte[] message in _v2)
        {
            string json = Encoding.UTF8.GetString(message);
            if (_v2Jetstream.TryDeriveV2Event(json, out AtJetstreamEvent? derived) && derived is not null)
            {
                parsed++;
            }
        }

        return parsed;
    }

    private int ParseV1(byte[] message)
    {
        string json = Encoding.UTF8.GetString(message);
        AtJetstreamEvent? atJetstreamEvent = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.AtJetstreamEvent);
        return atJetstreamEvent is not null && _v1Jetstream.DeriveEvent(atJetstreamEvent) is not null ? 1 : 0;
    }
}