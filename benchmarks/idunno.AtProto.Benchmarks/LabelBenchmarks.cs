// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;

using BenchmarkDotNet.Attributes;

using idunno.AtProto.Firehose;
using idunno.AtProto.Labels;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Measures decoding a signed moderation label from the labels firehose, and preparing it for signature verification.
/// </summary>
[MemoryDiagnoser]
public class LabelBenchmarks
{
    private byte[] _label = null!;

    [GlobalSetup]
    public void Setup() => _label = SignedLabel();

    [Benchmark]
    public Label Decode() => LabelEventDecoder.DecodeLabel(FirehoseCbor.ReadFields(_label, LabelEventDecoder.LabelFields));

    [Benchmark]
    public byte[] GetUnsignedLabel() => LabelEventDecoder.GetUnsignedLabel(_label);

    private static byte[] SignedLabel()
    {
        CborWriter writer = new(CborConformanceMode.Canonical);
        writer.WriteStartMap(7);
        writer.WriteTextString("cts");
        writer.WriteTextString("2026-09-28T01:02:03.456Z");
        writer.WriteTextString("sig");
        writer.WriteByteString(new byte[64]);
        writer.WriteTextString("src");
        writer.WriteTextString("did:plc:ar7c4by46qjdydhdevvrndac");
        writer.WriteTextString("uri");
        writer.WriteTextString("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.post/3l3qo2vuowo2b");
        writer.WriteTextString("val");
        writer.WriteTextString("spam");
        writer.WriteTextString("ver");
        writer.WriteInt32(1);
        writer.WriteTextString("neg");
        writer.WriteBoolean(false);
        writer.WriteEndMap();
        return writer.Encode();
    }
}
