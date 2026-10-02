// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;

using idunno.AtProto.Firehose;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Measures the firehose decode pipeline, per frame, one stage at a time.
/// </summary>
/// <remarks>
/// <para>Each stage includes the ones before it, so the cost of a stage is the difference from the previous one.</para>
/// </remarks>
[MemoryDiagnoser]
public class FirehoseBenchmarks
{
    private byte[][] _frames = null!;
    private RepoEventDecoder _decoder = null!;
    private CborFieldNames _payloadFields = null!;

    [GlobalSetup]
    public void Setup()
    {
        _frames = CorpusFile.Read(CorpusFile.Firehose);
        _decoder = new RepoEventDecoder(new FirehoseOptions(), verifier: null);
        _payloadFields = new CborFieldNames("error", "message", "name", "seq").Union(_decoder.PayloadFields);
    }

    [Benchmark(OperationsPerInvoke = CorpusFile.FirehoseMessageCount)]
    public int FrameParse()
    {
        int total = 0;
        foreach (byte[] frame in _frames)
        {
            total += FirehoseFrame.Parse(frame).Payload.Length;
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = CorpusFile.FirehoseMessageCount)]
    public int FrameParseAndReadFields()
    {
        int total = 0;
        foreach (byte[] frame in _frames)
        {
            total += FirehoseCbor.ReadFields(FirehoseFrame.Parse(frame).Payload, _payloadFields) is null ? 0 : 1;
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = CorpusFile.FirehoseMessageCount)]
    public async Task<int> FullDecode()
    {
        int total = 0;
        foreach (byte[] frame in _frames)
        {
            FirehoseFrame parsed = FirehoseFrame.Parse(frame);
            CborFields fields = FirehoseCbor.ReadFields(parsed.Payload, _payloadFields);
            if (parsed.Type is not null && _decoder.IsSequenced(parsed.Type))
            {
                FirehoseEvent decoded = await _decoder.DecodeAsync(parsed.Type, fields.GetInteger("seq"), fields, CancellationToken.None).ConfigureAwait(false);
                total += decoded is null ? 0 : 1;
            }
        }

        return total;
    }
}